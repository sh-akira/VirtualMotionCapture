using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniGLTF;
using UnityEngine;
using VRM;

namespace VMC
{
#if false
    public class VRMMetaImporter
    {
        public static async Task<VRMMetaObject> ImportVRMMeta(string path, bool createThumbnail = false)
        {
            byte[] buffer;
            using (FileStream SourceStream = File.Open(path, FileMode.Open))
            {
                var length = SourceStream.Length;

                if (length == 0)
                {
                    throw new Exception("empty bytes");
                }

                buffer = new byte[4];
                await SourceStream.ReadAsync(buffer, 0, buffer.Length);
                if (Encoding.ASCII.GetString(buffer, 0, 4) != glbImporter.GLB_MAGIC)
                {
                    throw new Exception("invalid magic");
                }

                await SourceStream.ReadAsync(buffer, 0, buffer.Length);
                var version = BitConverter.ToUInt32(buffer, 0);
                if (version != glbImporter.GLB_VERSION)
                {
                    Debug.LogWarningFormat("unknown version: {0}", version);
                    return null;
                }

                SourceStream.Seek(4, SeekOrigin.Current);

                await SourceStream.ReadAsync(buffer, 0, buffer.Length);
                var chunkDataSize = BitConverter.ToInt32(buffer, 0);

                await SourceStream.ReadAsync(buffer, 0, buffer.Length);
                var chunkTypeBytes = buffer.Where(x => x != 0).ToArray();
                var chunkTypeStr = Encoding.ASCII.GetString(chunkTypeBytes);
                var type = glbImporter.ToChunkType(chunkTypeStr);

                if (type != GlbChunkType.JSON)
                {
                    throw new Exception("chunk 0 is not JSON");
                }

                buffer = new byte[chunkDataSize];
                await SourceStream.ReadAsync(buffer, 0, buffer.Length);

                var context = new VRMImporterContext();
                context.Json = Encoding.UTF8.GetString(buffer);
                context.GLTF = JsonUtility.FromJson<glTF>(context.Json);

                if (context.GLTF.asset.version != "2.0")
                {
                    throw new UniGLTFException("unknown gltf version {0}", context.GLTF.asset.version);
                }

                /* Cannot call because private */
                //context.RestoreOlderVersionValues();

                //TODO: Is it necessary for the current VRM version?
                RestoreOlderVersionValues(context.Json, context.GLTF);

                if (createThumbnail)
                {
                    buffer = new byte[4];
                    await SourceStream.ReadAsync(buffer, 0, buffer.Length);
                    chunkDataSize = BitConverter.ToInt32(buffer, 0);

                    await SourceStream.ReadAsync(buffer, 0, buffer.Length);
                    chunkTypeBytes = buffer.Where(x => x != 0).ToArray();
                    chunkTypeStr = Encoding.ASCII.GetString(chunkTypeBytes);
                    type = glbImporter.ToChunkType(chunkTypeStr);

                    if (type != GlbChunkType.BIN)
                    {
                        throw new Exception("chunk 1 is not BIN");
                    }

                    buffer = new byte[chunkDataSize];
                    await SourceStream.ReadAsync(buffer, 0, buffer.Length);

                    var storage = new SimpleStorage(new ArraySegment<byte>(buffer));
                    foreach (var gltfbuffer in context.GLTF.buffers)
                    {
                        gltfbuffer.OpenStorage(storage);
                    }
                }

                return context.ReadMeta(createThumbnail);
            }
        }

        //from ImporterContext.cs(UniGLTF)
        static void RestoreOlderVersionValues(string Json, glTF GLTF)
        {
            var parsed = UniJSON.JsonParser.Parse(Json);
            for (int i = 0; i < GLTF.images.Count; ++i)
            {
                if (string.IsNullOrEmpty(GLTF.images[i].name))
                {
                    try
                    {
                        var extraName = parsed["images"][i]["extra"]["name"].Value.GetString();
                        if (!string.IsNullOrEmpty(extraName))
                        {
                            //Debug.LogFormat("restore texturename: {0}", extraName);
                            GLTF.images[i].name = extraName;
                        }
                    }
                    catch (Exception)
                    {
                        // do nothing
                    }
                }
            }
            for (int i = 0; i < GLTF.meshes.Count; ++i)
            {
                var mesh = GLTF.meshes[i];
                try
                {
                    for (int j = 0; j < mesh.primitives.Count; ++j)
                    {
                        var primitive = mesh.primitives[j];
                        for (int k = 0; k < primitive.targets.Count; ++k)
                        {
                            var extraName = parsed["meshes"][i]["primitives"][j]["targets"][k]["extra"]["name"].Value.GetString();
                            //Debug.LogFormat("restore morphName: {0}", extraName);
                            primitive.extras.targetNames.Add(extraName);
                        }
                    }
                }
                catch (Exception)
                {
                    // do nothing
                }
            }
#if false
            for (int i = 0; i < GLTF.nodes.Count; ++i)
            {
                var node = GLTF.nodes[i];
                try
                {
                    var extra = parsed["nodes"][i]["extra"]["skinRootBone"].AsInt;
                    //Debug.LogFormat("restore extra: {0}", extra);
                    //node.extras.skinRootBone = extra;
                }
                catch (Exception)
                {
                    // do nothing
                }
            }
#endif
        }
    }
#endif
}