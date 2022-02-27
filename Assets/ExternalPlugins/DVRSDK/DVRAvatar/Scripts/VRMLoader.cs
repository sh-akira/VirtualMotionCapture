using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
#if UNIVRM_0_68_IMPORTER || UNIVRM_0_77_IMPORTER
using UniGLTF;
#endif
using UnityEngine;
using VRM;

namespace DVRSDK.Avatar
{
    public class VRMLoader : IDisposable
    {
#if UNIVRM_LEGACY_IMPORTER || UNIVRM_0_68_IMPORTER
        private VRMImporterContext currentContext;
        public GameObject Model => currentContext == null ? null : currentContext.Root;
#elif UNIVRM_0_77_IMPORTER
        private VRMImporterContext currentContext;
        private RuntimeGltfInstance currentInstance;
        public GameObject Model => currentInstance == null ? null : currentInstance.Root;
#else
        private IDisposable currentContext = null;
        public GameObject Model = null;
#endif


        /// <summary>
        /// 読み込んだモデルを実際に表示する
        /// </summary>
        public void ShowMeshes()
        {
#if UNIVRM_LEGACY_IMPORTER || UNIVRM_0_68_IMPORTER || UNIVRM_0_77_IMPORTER
            if (Model == null)
                throw new InvalidOperationException("Need to load VRM model first.");
#endif

#if UNIVRM_LEGACY_IMPORTER || UNIVRM_0_68_IMPORTER
            currentContext.ShowMeshes();
#elif UNIVRM_0_77_IMPORTER
            currentInstance.ShowMeshes();
#else
#endif

#if UNIVRM_0_68_IMPORTER
            currentContext.DisposeOnGameObjectDestroyed();
#endif
        }

        /// <summary>
        /// 同期でファイルからVRMモデルを読み込む
        /// </summary>
        /// <param name="vrmFilePath">ファイルのパス</param>
        /// <returns>VRMモデルのGameObject</returns>
        public GameObject LoadVrmModelFromFile(string vrmFilePath)
        {
            // ファイルをByte配列に読み込みます
            var bytes = File.ReadAllBytes(vrmFilePath);

            return LoadVrmModelFromByteArray(bytes);
        }

        /// <summary>
        /// 同期でファイルからVRMMetaObjectを読み込む
        /// </summary>
        /// <param name="vrmByteArray"></param>
        /// <param name="createThumbnail"></param>
        /// <returns></returns>
        public VRMMetaObject LoadVrmMetaFromFile(string vrmFilePath, bool createThumbnail)
        {
            // ファイルをByte配列に読み込みます
            var bytes = File.ReadAllBytes(vrmFilePath);

            return LoadVrmMetaFromByteArray(bytes, createThumbnail);
        }

        /// <summary>
        /// 非同期でファイルからVRMモデルを読み込む
        /// </summary>
        /// <param name="vrmFilePath">ファイルのパス</param>
        /// <returns>VRMモデルのGameObject</returns>
        public async Task<GameObject> LoadVrmModelFromFileAsync(string vrmFilePath)
        {
            // ファイルをByte配列に読み込みます
            var bytes = await ReadAllBytesAsync(vrmFilePath);

            return await LoadVrmModelFromByteArrayAsync(bytes);
        }

        /// <summary>
        /// 非同期でファイルからVRMMetaObjectを読み込む
        /// </summary>
        /// <param name="vrmByteArray"></param>
        /// <param name="createThumbnail"></param>
        /// <returns></returns>
        public async Task<VRMMetaObject> LoadVrmMetaFromFileAsync(string vrmFilePath, bool createThumbnail)
        {
            // ファイルをByte配列に読み込みます
            var bytes = await ReadAllBytesAsync(vrmFilePath);

            return await LoadVrmMetaFromByteArrayAsync(bytes, createThumbnail);
        }

        /// <summary>
        /// 同期でByte配列からVRMモデルを読み込む
        /// </summary>
        /// <param name="vrmByteArray"></param>
        /// <returns></returns>
        public GameObject LoadVrmModelFromByteArray(byte[] vrmByteArray)
        {
#if UNIVRM_LEGACY_IMPORTER
            InitializeVrmContextFromByteArray(vrmByteArray);

            // 同期処理で読み込みます
            currentContext.Load();

            // 読込が完了するとcontext.RootにモデルのGameObjectが入っています
            var root = currentContext.Root;

            return root;
#elif UNIVRM_0_68_IMPORTER
            var parser = new GltfParser();
            parser.ParseGlb(vrmByteArray);

            currentContext = new VRMImporterContext(parser);
            currentContext.Load();

            return currentContext.Root;
#elif UNIVRM_0_77_IMPORTER
            var parser = new GlbLowLevelParser(string.Empty, vrmByteArray);
            var data = parser.Parse();
            
            currentContext = new VRMImporterContext(data);
            currentInstance = currentContext.Load();

            return currentInstance.Root;
#else
            return null;
#endif
        }

        /// <summary>
        /// 同期でByte配列からVRMMetaObjectを読み込む
        /// </summary>
        /// <param name="vrmByteArray"></param>
        /// <param name="createThumbnail"></param>
        /// <returns></returns>
        public VRMMetaObject LoadVrmMetaFromByteArray(byte[] vrmByteArray, bool createThumbnail)
        {
            InitializeVrmContextFromByteArray(vrmByteArray);

            return GetMeta(createThumbnail);
        }

        /// <summary>
        /// ConnectからロードしたモデルデータをVRM化する
        /// </summary>
        /// <param name="cachedData"></param>
        /// <returns></returns>
        public object LoadVRMModelFromConnect(byte[] cachedData)
        {
            return LoadVrmModelFromByteArray(cachedData);
        }

        /// <summary>
        /// 非同期でByte配列からVRMモデルを読み込む
        /// </summary>
        /// <param name="vrmByteArray"></param>
        /// <returns></returns>
        public async Task<GameObject> LoadVrmModelFromByteArrayAsync(byte[] vrmByteArray)
        {
#if UNIVRM_LEGACY_IMPORTER
            await InitializeVrmContextFromByteArrayAsync(vrmByteArray);

            // 非同期処理(Task)で読み込みます
            await currentContext.LoadAsyncTask();

            // 読込が完了するとcontext.RootにモデルのGameObjectが入っています
            var root = currentContext.Root;

            return root;
#elif UNIVRM_0_68_IMPORTER
            var parser = new GltfParser();
            await Task.Run(() =>
            {
                parser.ParseGlb(vrmByteArray);
            });

            currentContext = new VRMImporterContext(parser);
            await currentContext.LoadAsync();

            return currentContext.Root;
#elif UNIVRM_0_77_IMPORTER
            var parser = new GlbLowLevelParser(string.Empty, vrmByteArray);
            GltfData data = null;

            await Task.Run(() =>
            {
                data = parser.Parse();
            });

            currentContext = new VRMImporterContext(data);
            currentInstance = await currentContext.LoadAsync();

            return currentInstance.Root;
#else
            return null;
#endif
        }

        /// <summary>
        /// 非同期でByte配列からVRMMetaObjectを読み込む
        /// </summary>
        /// <param name="vrmByteArray"></param>
        /// <param name="createThumbnail"></param>
        /// <returns></returns>
        public async Task<VRMMetaObject> LoadVrmMetaFromByteArrayAsync(byte[] vrmByteArray, bool createThumbnail)
        {
            await InitializeVrmContextFromByteArrayAsync(vrmByteArray);

            return GetMeta(createThumbnail);
        }

        /// <summary>
        /// ConnectからロードしたモデルデータをVRM化する
        /// </summary>
        /// <param name="cachedData"></param>
        /// <returns></returns>
        public async Task<object> LoadVRMModelFromConnectAsync(byte[] cachedData)
        {
            return await LoadVrmModelFromByteArrayAsync(cachedData);
        }

        /// <summary>
        /// Byte配列からVRMImporterContextの初期化をします
        /// </summary>
        /// <param name="vrmByteArray"></param>
        public void InitializeVrmContextFromByteArray(byte[] vrmByteArray)
        {
#if UNIVRM_LEGACY_IMPORTER
            // VRMImporterContextがVRMを読み込む機能を提供します
            currentContext = new VRMImporterContext();

            // GLB形式でJSONを取得しParseします
            currentContext.ParseGlb(vrmByteArray);
#elif UNIVRM_0_68_IMPORTER
            var parser = new GltfParser();
            parser.ParseGlb(vrmByteArray);
            currentContext = new VRMImporterContext(parser);
#else
#endif
        }

        /// <summary>
        /// 非同期でByte配列からVRMImporterContextの初期化をします
        /// </summary>
        /// <param name="vrmByteArray"></param>
        public async Task InitializeVrmContextFromByteArrayAsync(byte[] vrmByteArray)
        {
#if UNIVRM_LEGACY_IMPORTER
            // VRMImporterContextがVRMを読み込む機能を提供します
            currentContext = new VRMImporterContext();

            // GLB形式でJSONを取得しParseします
            await Task.Run(() => currentContext.ParseGlb(vrmByteArray));
#elif UNIVRM_0_68_IMPORTER
            var parser = new GltfParser();
            await Task.Run(() => parser.ParseGlb(vrmByteArray));
            currentContext = new VRMImporterContext(parser);
#elif UNIVRM_0_77_IMPORTER
            var parser = new GlbLowLevelParser(string.Empty, vrmByteArray);
            GltfData data = null;

            await Task.Run(() =>
            {
                data = parser.Parse();
            });

            currentContext = new VRMImporterContext(data);
            currentInstance = null;
#else
#endif
        }

        /// <summary>
        /// Metaデータの読み出し
        /// </summary>
        /// <param name="createThumbnail">サムネイルを作成するかどうか</param>
        /// <returns>VRMMetaObject</returns>
        public VRMMetaObject GetMeta(bool createThumbnail)
        {
#if UNIVRM_LEGACY_IMPORTER || UNIVRM_0_68_IMPORTER || UNIVRM_0_77_IMPORTER
            if (currentContext == null)
                throw new InvalidOperationException("Need to initialize VRM model first.");
            return currentContext.ReadMeta(createThumbnail);
#else
            return null;
#endif
        }

        // Byte列を得る
        public async static Task<byte[]> ReadAllBytesAsync(string path)
        {
            byte[] result;
            using (FileStream SourceStream = File.Open(path, FileMode.Open))
            {
                result = new byte[SourceStream.Length];
                await SourceStream.ReadAsync(result, 0, (int)SourceStream.Length);
            }
            return result;
        }

        public void SetupFirstPerson(Camera firstPersonCamera)
        {
            // HMDに顔が映りこまないようにFirstPersonの初期化
            var vrmFirstPerson = Model.GetComponent<VRMFirstPerson>();
            if (vrmFirstPerson != null) vrmFirstPerson.Setup();

            foreach (var camera in GameObject.FindObjectsOfType<Camera>())
            {
                camera.cullingMask = (camera == firstPersonCamera)
                    ? camera.cullingMask & ~(1 << VRMFirstPerson.THIRDPERSON_ONLY_LAYER) // ThirdPersonだけ無効
                    : camera.cullingMask & ~(1 << VRMFirstPerson.FIRSTPERSON_ONLY_LAYER) // FirstPersonだけ無効
                    ;
            }
        }

        public void Dispose()
        {
            currentContext?.Dispose();
        }
    }
}
