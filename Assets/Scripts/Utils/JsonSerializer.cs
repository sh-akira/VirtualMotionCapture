using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace sh_akira
{
    public class Json
    {
        public class Serializer
        {
            public static string Serialize(object target)
            {
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(target.GetType());

                    serializer.WriteObject(stream, target);
                    var arr = stream.ToArray();
                    return Encoding.UTF8.GetString(arr, 0, arr.Length);
                }
            }

            public static string ToReadable(string json)
            {
                if (string.IsNullOrWhiteSpace(json)) return json;
                int i = 0;
                int indent = 0;
                int quoteCount = 0;
                int position = -1;
                var sb = new StringBuilder();
                int lastindex = 0;
                while (true)
                {
                    if (i > 0 && json[i] == '"' && json[i - 1] != '\\') quoteCount++;

                    if (quoteCount % 2 == 0) //is not value(quoted)
                    {
                        if (json[i] == '{' || json[i] == '[')
                        {
                            indent++;
                            position = 1;
                        }
                        else if (json[i] == '}' || json[i] == ']')
                        {
                            indent--;
                            position = 0;
                        }
                        else if (json.Length > i && json[i] == ',' && json[i + 1] == '"')
                        {
                            position = 1;
                        }
                        if (position >= 0)
                        {
                            sb.AppendLine(json.Substring(lastindex, i + position - lastindex));
                            sb.Append(new string(' ', indent * 4));
                            lastindex = i + position;
                            position = -1;
                        }
                    }

                    i++;
                    if (json.Length <= i)
                    {
                        sb.Append(json.Substring(lastindex));
                        break;
                    }

                }
                return sb.ToString();
            }

            public static string ToReadableOld(string json)
            {
                if (string.IsNullOrWhiteSpace(json)) return json;
                int i = 0;
                int indent = 0;
                int quoteCount = 0;
                int position = -1;
                string indentStr = "\r\n";
                while (true)
                {
                    if (i > 0 && json[i] == '"' && json[i - 1] != '\\') quoteCount++;

                    if (quoteCount % 2 == 0) //is not value(quoted)
                    {
                        if (json[i] == '{' || json[i] == '[')
                        {
                            indent++;
                            position = 1;
                        }
                        else if (json[i] == '}' || json[i] == ']')
                        {
                            indent--;
                            position = 0;
                        }
                        else if (json.Length > i && json[i] == ',' && json[i + 1] == '"')
                        {
                            position = 1;
                        }
                        if (position >= 0)
                        {
                            indentStr = "\r\n" + new string(' ', indent * 4);
                            json = json.Substring(0, i + position) + indentStr + json.Substring(i + position);
                            i += indentStr.Length - position;
                            position = -1;
                        }
                    }

                    i++;
                    if (json.Length <= i) break;
                }
                return json;
            }


            public static T Deserialize<T>(string json)
            {
                return (T)Deserialize(typeof(T), json);
            }

            public static object Deserialize(Type type, string json)
            {
                if (string.IsNullOrWhiteSpace(json)) json = "{}";
                //Debug.Log("***Deserialize:" + json);
                try
                {
                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    {
                        var serializer = new DataContractJsonSerializer(type);
                        return serializer.ReadObject(stream);
                    }
                }
                catch (Exception)//System.Runtime.Serialization.SerializationException
                {
                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json.Replace("\r", "").Replace("\n", "\\r\\n"))))
                    {
                        var serializer = new DataContractJsonSerializer(type);
                        return serializer.ReadObject(stream);
                    }
                }
            }
        }
    }
}
