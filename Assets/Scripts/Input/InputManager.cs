using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityMemoryMappedFile;

namespace VMC
{
    public class InputManager : MonoBehaviour
    {
        private static InputManager current;
        public static InputManager Current => current;

        public ControlWPFWindow controlWPFWindow;

        public SteamVR2Input steamVR2Input;

        public MidiCCWrapper midiCCWrapper;

        private System.Threading.SynchronizationContext context = null;

        private void Awake()
        {
            current = this;
        }

        private void Start()
        {
            context = System.Threading.SynchronizationContext.Current;

            controlWPFWindow.server.ReceivedEvent += Server_Received;

            steamVR2Input.KeyDownEvent += ControllerAction_KeyDown;
            steamVR2Input.KeyUpEvent += ControllerAction_KeyUp;
            steamVR2Input.AxisChangedEvent += ControllerAction_AxisChanged;

            KeyboardAction.KeyDownEvent += KeyboardAction_KeyDown;
            KeyboardAction.KeyUpEvent += KeyboardAction_KeyUp;


            midiCCWrapper.noteOnDelegateProxy += async (channel, note, velocity) =>
            {
                Debug.Log("MidiNoteOn:" + channel + "/" + note + "/" + velocity);

                var config = new KeyConfig();
                config.type = KeyTypes.Midi;
                config.actionType = KeyActionTypes.Face;
                config.keyCode = (int)channel;
                config.keyIndex = note;
                config.keyName = MidiName(channel, note);
                if (doKeyConfig || doKeySend) await controlWPFWindow.server.SendCommandAsync(new PipeCommands.KeyDown { Config = config });
                if (!doKeyConfig) CheckKey(config, true);
            };

            midiCCWrapper.noteOffDelegateProxy += (channel, note) =>
            {
                Debug.Log("MidiNoteOff:" + channel + "/" + note);

                var config = new KeyConfig();
                config.type = KeyTypes.Midi;
                config.actionType = KeyActionTypes.Face;
                config.keyCode = (int)channel;
                config.keyIndex = note;
                config.keyName = MidiName(channel, note);
                if (doKeyConfig || doKeySend) { }//  await server.SendCommandAsync(new PipeCommands.KeyUp { Config = config });
                if (!doKeyConfig) CheckKey(config, false);
            };
            midiCCWrapper.knobUpdateBoolDelegate += async (int knobNo, bool value) =>
            {
                MidiJack.MidiChannel channel = MidiJack.MidiChannel.Ch1; //仮でCh1
                Debug.Log("MidiCC:" + channel + "/" + knobNo + "/" + value);

                var config = new KeyConfig();
                config.type = KeyTypes.MidiCC;
                config.actionType = KeyActionTypes.Face;
                config.keyCode = (int)channel;
                config.keyIndex = knobNo;
                config.keyName = MidiName(channel, knobNo);

                if (value)
                {
                    if (doKeyConfig || doKeySend) await controlWPFWindow.server.SendCommandAsync(new PipeCommands.KeyDown { Config = config });
                }
                else
                {
                    if (doKeyConfig || doKeySend) { }//  await server.SendCommandAsync(new PipeCommands.KeyUp { Config = config });
                }
                if (!doKeyConfig) CheckKey(config, value);
            };

            midiCCWrapper.knobDelegateProxy += (MidiJack.MidiChannel channel, int knobNo, float value) =>
            {
                CheckKnobUpdated(channel, knobNo, value);
            };

        }

        private void OnApplicationQuit()
        {
            KeyboardAction.KeyDownEvent -= KeyboardAction_KeyDown;
            KeyboardAction.KeyUpEvent -= KeyboardAction_KeyUp;

            steamVR2Input.KeyDownEvent -= ControllerAction_KeyDown;
            steamVR2Input.KeyUpEvent -= ControllerAction_KeyUp;
            steamVR2Input.AxisChangedEvent -= ControllerAction_AxisChanged;

            controlWPFWindow.server.ReceivedEvent -= Server_Received;
        }

        private void Server_Received(object sender, DataReceivedEventArgs e)
        {
            context.Post(async s =>
            {
                if (e.CommandType == typeof(PipeCommands.SetSkeletalInputEnable))
                {
                    var d = (PipeCommands.SetSkeletalInputEnable)e.Data;
                    Settings.Current.EnableSkeletal = d.enable;
                    SteamVR2Input.EnableSkeletal = Settings.Current.EnableSkeletal;
                }
                else if (e.CommandType == typeof(PipeCommands.StartKeyConfig))
                {
                    doKeyConfig = true;
                    controlWPFWindow.faceController.StartSetting();
                    CurrentKeyConfigs.Clear();
                }
                else if (e.CommandType == typeof(PipeCommands.EndKeyConfig))
                {
                    controlWPFWindow.faceController.EndSetting();
                    doKeyConfig = false;
                    CurrentKeyConfigs.Clear();
                }
                else if (e.CommandType == typeof(PipeCommands.StartKeySend))
                {
                    doKeySend = true;
                    CurrentKeyConfigs.Clear();
                }
                else if (e.CommandType == typeof(PipeCommands.EndKeySend))
                {
                    doKeySend = false;
                    CurrentKeyConfigs.Clear();
                }
                else if (e.CommandType == typeof(PipeCommands.SetKeyActions))
                {
                    var d = (PipeCommands.SetKeyActions)e.Data;
                    Settings.Current.KeyActions = d.KeyActions;
                }
            }, null);
        }

        private string MidiName(MidiJack.MidiChannel channel, int note)
        {
            return $"MIDI Ch{(int)channel + 1} {note}";
        }

        private float[] lastKnobUpdatedSendTime = new float[MidiCCWrapper.KNOBS];

        private async void CheckKnobUpdated(MidiJack.MidiChannel channel, int knobNo, float value)
        {
            if (doKeySend == false) return;
            if (lastKnobUpdatedSendTime[knobNo] + 3f < Time.realtimeSinceStartup)
            {
                lastKnobUpdatedSendTime[knobNo] = Time.realtimeSinceStartup;
                await controlWPFWindow.server?.SendCommandAsync(new PipeCommands.MidiCCKnobUpdate { channel = (int)channel, knobNo = knobNo, value = value });
            }
        }

        private bool doKeyConfig = false;
        private bool doKeySend = false;

        private async void ControllerAction_KeyDown(object sender, OVRKeyEventArgs e)
        {
            //win.KeyDownEvent{ value = win, new KeyEventArgs((EVRButtonId)e.ButtonId, e.Axis.x, e.Axis.y, e.IsLeft));

            var config = new KeyConfig();
            config.type = KeyTypes.Controller;
            config.actionType = KeyActionTypes.Hand;
            config.keyCode = -2;
            config.keyName = e.Name;
            config.isLeft = e.IsLeft;
            bool isStick = e.Name.Contains("Stick");
            config.keyIndex = e.IsAxis == false ? -1 : NearestPointIndex(e.IsLeft, e.Axis.x, e.Axis.y, isStick);
            config.isTouch = e.IsTouch;
            if (e.IsAxis)
            {
                if (config.keyIndex < 0) return;
                if (e.IsLeft)
                {
                    if (isStick) lastStickLeftAxisPoint = config.keyIndex;
                    else lastTouchpadLeftAxisPoint = config.keyIndex;
                }
                else
                {
                    if (isStick) lastStickRightAxisPoint = config.keyIndex;
                    else lastTouchpadRightAxisPoint = config.keyIndex;
                }
            }
            if (doKeyConfig || doKeySend) await controlWPFWindow.server.SendCommandAsync(new PipeCommands.KeyDown { Config = config });
            if (!doKeyConfig) CheckKey(config, true);
        }

        private async void ControllerAction_KeyUp(object sender, OVRKeyEventArgs e)
        {
            //win.KeyUpEvent{ value = win, new KeyEventArgs((EVRButtonId)e.ButtonId, e.Axis.x, e.Axis.y, e.IsLeft));
            var config = new KeyConfig();
            config.type = KeyTypes.Controller;
            config.actionType = KeyActionTypes.Hand;
            config.keyCode = -2;
            config.keyName = e.Name;
            config.isLeft = e.IsLeft;
            bool isStick = e.Name.Contains("Stick");
            config.keyIndex = e.IsAxis == false ? -1 : NearestPointIndex(e.IsLeft, e.Axis.x, e.Axis.y, isStick);
            config.isTouch = e.IsTouch;
            if (e.IsAxis && config.keyIndex != (isStick ? (e.IsLeft ? lastStickLeftAxisPoint : lastStickRightAxisPoint) : (e.IsLeft ? lastTouchpadLeftAxisPoint : lastTouchpadRightAxisPoint)))
            {//タッチパッド離した瞬間違うポイントだった場合
                var newindex = config.keyIndex;
                config.keyIndex = (isStick ? (e.IsLeft ? lastStickLeftAxisPoint : lastStickRightAxisPoint) : (e.IsLeft ? lastTouchpadLeftAxisPoint : lastTouchpadRightAxisPoint));
                //前のキーを離す
                if (doKeyConfig) { }//  await server.SendCommandAsync(new PipeCommands.KeyUp { Config = config });
                else CheckKey(config, false);
                config.keyIndex = newindex;
                if (config.keyIndex < 0) return;
                //新しいキーを押す
                if (doKeyConfig) await controlWPFWindow.server.SendCommandAsync(new PipeCommands.KeyDown { Config = config });
                else CheckKey(config, true);
            }
            if (doKeyConfig || doKeySend) { }//  await server.SendCommandAsync(new PipeCommands.KeyUp { Config = config });
            if (!doKeyConfig) CheckKey(config, false);
        }

        private int lastTouchpadLeftAxisPoint = -1;
        private int lastTouchpadRightAxisPoint = -1;
        private int lastStickLeftAxisPoint = -1;
        private int lastStickRightAxisPoint = -1;

        private bool isSendingKey = false;
        //タッチパッドやアナログスティックの変動
        private async void ControllerAction_AxisChanged(object sender, OVRKeyEventArgs e)
        {
            if (e.IsAxis == false) return;
            var keyName = e.Name;
            if (keyName.Contains("Trigger")) return; //トリガーは現時点ではアナログ入力無効
            if (keyName.Contains("Position")) keyName = keyName.Replace("Position", "Touch"); //ポジションはいったんタッチと同じにする
            bool isStick = keyName.Contains("Stick");
            var newindex = NearestPointIndex(e.IsLeft, e.Axis.x, e.Axis.y, isStick);
            if ((isStick ? (e.IsLeft ? lastStickLeftAxisPoint : lastStickRightAxisPoint) : (e.IsLeft ? lastTouchpadLeftAxisPoint : lastTouchpadRightAxisPoint)) != newindex)
            {//ドラッグで隣の領域に入った場合
                var config = new KeyConfig();
                config.type = KeyTypes.Controller;
                config.actionType = KeyActionTypes.Hand;
                config.keyCode = -2;
                config.keyName = keyName;
                config.isLeft = e.IsLeft;
                config.keyIndex = (isStick ? (e.IsLeft ? lastStickLeftAxisPoint : lastStickRightAxisPoint) : (e.IsLeft ? lastTouchpadLeftAxisPoint : lastTouchpadRightAxisPoint));
                config.isTouch = true;// e.IsTouch; //ポジションはいったんタッチと同じにする
                                      //前のキーを離す
                if (doKeyConfig || doKeySend) { }//  await server.SendCommandAsync(new PipeCommands.KeyUp { Config = config });
                if (!doKeyConfig) CheckKey(config, false);
                config.keyIndex = newindex;
                //新しいキーを押す
                if (doKeyConfig || doKeySend)
                {
                    if (isSendingKey == false)
                    {
                        isSendingKey = true;
                        await controlWPFWindow.server.SendCommandAsync(new PipeCommands.KeyDown { Config = config });
                        isSendingKey = false;
                    }
                }
                if (!doKeyConfig) CheckKey(config, true);
                if (e.IsLeft)
                {
                    if (isStick) lastStickLeftAxisPoint = newindex;
                    else lastTouchpadLeftAxisPoint = newindex;
                }
                else
                {
                    if (isStick) lastStickRightAxisPoint = newindex;
                    else lastTouchpadRightAxisPoint = newindex;
                }
            }
        }


        private async void KeyboardAction_KeyDown(object sender, KeyboardEventArgs e)
        {
            var config = new KeyConfig();
            config.type = KeyTypes.Keyboard;
            config.actionType = KeyActionTypes.Face;
            config.keyCode = e.KeyCode;
            config.keyName = e.KeyName;
            if (doKeyConfig || doKeySend) await controlWPFWindow.server.SendCommandAsync(new PipeCommands.KeyDown { Config = config });
            if (!doKeyConfig) CheckKey(config, true);
        }

        private /*async*/ void KeyboardAction_KeyUp(object sender, KeyboardEventArgs e)
        {
            var config = new KeyConfig();
            config.type = KeyTypes.Keyboard;
            config.actionType = KeyActionTypes.Face;
            config.keyCode = e.KeyCode;
            config.keyName = e.KeyName;
            if (doKeyConfig || doKeySend) { }//  await server.SendCommandAsync(new PipeCommands.KeyUp { Config = config });
            if (!doKeyConfig) CheckKey(config, false);
        }

        private int NearestPointIndex(bool isLeft, float x, float y, bool isStick)
        {
            //Debug.Log($"SearchNearestPoint:{x},{y},{isLeft}");
            int index = 0;
            var points = isStick ? (isLeft ? Settings.Current.LeftThumbStickPoints : Settings.Current.RightThumbStickPoints) : (isLeft ? Settings.Current.LeftTouchPadPoints : Settings.Current.RightTouchPadPoints);
            if (points == null) return 0; //未設定時は一つ
            var centerEnable = isLeft ? Settings.Current.LeftCenterEnable : Settings.Current.RightCenterEnable;
            if (centerEnable || isStick) //センターキー有効時(タッチパッド) / スティックの場合はセンター無効にする
            {
                var point_distance = x * x + y * y;
                var r = 2.0f / 5.0f; //半径
                var r2 = r * r;
                if (point_distance < r2) //円内
                {
                    if (isStick) return -1;
                    index = points.Count + 1;
                    return index;
                }
            }
            double maxlength = double.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                double length = Math.Sqrt(Math.Pow(x - p.x, 2) + Math.Pow(y - p.y, 2));
                if (maxlength > length)
                {
                    maxlength = length;
                    index = i + 1;
                }
            }
            return index;
        }

        private List<KeyConfig> CurrentKeyConfigs = new List<KeyConfig>();
        private List<KeyAction> CurrentKeyUpActions = new List<KeyAction>();

        private void CheckKey(KeyConfig config, bool isKeyDown)
        {
            if (Settings.Current.KeyActions == null) return;
            if (isKeyDown)
            {
                //CurrentKeyConfigs.Clear();
                CurrentKeyConfigs.Add(config);
                Debug.Log("押:" + config.ToString());
                var doKeyActions = new List<KeyAction>();
                foreach (var action in Settings.Current.KeyActions?.OrderBy(d => d.KeyConfigs.Count()))
                {//キーの少ない順に実行して、同時押しと被ったとき同時押しを後から実行して上書きさせる
                 //if (action.KeyConfigs.Count == CurrentKeyConfigs.Count)
                 //{ //別々の機能を同時に押す場合もあるのでキーの数は見てはいけない
                    var enable = true;
                    foreach (var key in action.KeyConfigs)
                    {
                        if (CurrentKeyConfigs.Where(d => d.IsEqualKeyCode(key) == true).Any() == false)
                        {
                            //キーが含まれてないとき
                            enable = false;
                        }
                    }
                    if (enable)
                    {//現在押してるキーの中にすべてのキーが含まれていた
                        if (action.IsKeyUp)
                        {
                            //キーを離す操作の時はキューに入れておく
                            CurrentKeyUpActions.Add(action);
                        }
                        else
                        {
                            doKeyActions.Add(action);
                        }
                    }
                    //}
                }
                if (doKeyActions.Any())
                {
                    var tmpActions = new List<KeyAction>(doKeyActions);
                    foreach (var action in tmpActions)
                    {
                        foreach (var target in tmpActions.Where(d => d != action))
                        {
                            if (target.KeyConfigs.ContainsArray(action.KeyConfigs))
                            {//更に複数押しのキー設定が有効な場合、少ないほうは無効(上書きされてしまうため)
                                doKeyActions.Remove(action);
                            }
                        }
                    }
                    foreach (var action in doKeyActions)
                    {//残った処理だけ実行
                        controlWPFWindow.DoKeyAction(action);
                    }
                }

            }
            else
            {
                CurrentKeyConfigs.RemoveAll(d => d.IsEqualKeyCode(config)); //たまに離し損ねるので、もし押しっぱなしなら削除
                Debug.Log("離:" + config.ToString());
                //キーを離すイベントのキューチェック
                var tmpActions = new List<KeyAction>(CurrentKeyUpActions);
                foreach (var action in tmpActions)
                {//1度押されたキーなので、現在押されてないキーなら離れたことになる
                    var enable = true;
                    foreach (var key in action.KeyConfigs)
                    {
                        if (CurrentKeyConfigs.Where(d => d.IsEqualKeyCode(key) == true).Any() == true) //まだ押されてる
                        {
                            enable = false;
                        }
                    }
                    if (enable)
                    {
                        //手の操作の場合、別の手の操作キーが押されたままだったらそちらを優先して、離す処理は飛ばす
                        var skipKeyUp = false;

                        var doKeyActions = new List<KeyAction>();
                        //手の操作時は左手と右手は分けて処理しないと、右がおしっぱで左を離したときに戻らなくなる
                        foreach (var downaction in Settings.Current.KeyActions?.OrderBy(d => d.KeyConfigs.Count()).Where(d => d.FaceAction == action.FaceAction && d.HandAction == action.HandAction && d.Hand == action.Hand && d.FunctionAction == action.FunctionAction))
                        {//キーの少ない順に実行して、同時押しと被ったとき同時押しを後から実行して上書きさせる
                         //if (action.KeyConfigs.Count == CurrentKeyConfigs.Count)
                         //{ //別々の機能を同時に押す場合もあるのでキーの数は見てはいけない
                            var downenable = true;
                            foreach (var key in downaction.KeyConfigs)
                            {
                                if (CurrentKeyConfigs.Where(d => d.IsEqualKeyCode(key) == true).Any() == false)
                                {
                                    //キーが含まれてないとき
                                    downenable = false;
                                }
                            }
                            if (downenable)
                            {//現在押してるキーの中にすべてのキーが含まれていた
                                if (downaction.IsKeyUp)
                                {
                                }
                                else
                                {
                                    doKeyActions.Add(downaction);
                                }
                            }
                            //}
                        }
                        if (doKeyActions.Any())
                        {
                            skipKeyUp = true; //優先処理があったので、KeyUpのActionは無効
                            var tmpDownActions = new List<KeyAction>(doKeyActions);
                            foreach (var downaction in tmpDownActions)
                            {
                                foreach (var target in tmpActions.Where(d => d != downaction))
                                {
                                    if (target.KeyConfigs.ContainsArray(downaction.KeyConfigs))
                                    {//更に複数押しのキー設定が有効な場合、少ないほうは無効(上書きされてしまうため)
                                        doKeyActions.Remove(downaction);
                                    }
                                }
                            }
                            foreach (var downaction in doKeyActions)
                            {//残った処理だけ実行
                                controlWPFWindow.DoKeyAction(downaction);
                            }
                        }
                        if (skipKeyUp == false) controlWPFWindow.DoKeyAction(action);
                        CurrentKeyUpActions.Remove(action);
                    }
                }
            }
        }
    }

    public enum MouseButtons
    {
        Left = 0,
        Right = 1,
        Center = 2,
    }
}