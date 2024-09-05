﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TMMLibrary.Converters;
using TMMLibrary.TMM;
using TMMViewer.Data.Render;
using TMMViewer.ViewModels.MonoGameControls;
using TmmVector3 = System.Numerics.Vector3;
using TmmVector2 = System.Numerics.Vector2;
using System.IO;

namespace TMMViewer.Data.Services
{
    public class IOModelService : IModelIOService
    {
        private Scene _scene;
        public TmmFile tmmFile = new();
        public List<TmmDataFile> tmmDataFiles = new();
        public string? OpenedModelPath { get; private set; }

        private IMonoGameViewModel _monoGame;

        public IOModelService(Scene scene, IMonoGameViewModel monoGame)
        {
            _scene = scene;
            _monoGame = monoGame;
        }

        public void ImportModel(string path)
        {
            var graphicsDevice = _monoGame.GraphicsDeviceService.GraphicsDevice;
            var _content = _monoGame.Content;

            _scene.Meshes.Clear();
            tmmDataFiles.Clear();
            tmmFile = TmmFile.Decode(path);
            OpenedModelPath = path;

            var yMax = float.MinValue;
            var yMin = float.MaxValue;

            ushort[] iMax = { ushort.MinValue, ushort.MinValue, ushort.MinValue };
            ushort[] iMin = { ushort.MaxValue, ushort.MaxValue, ushort.MaxValue };

            foreach (var model in tmmFile.ModelInfo)
            {
                var data = TmmDataFile.Decode(model, path + ".data");
                tmmDataFiles.Add(data);
                
                var vertices = data.Vertices.Select(
                    v =>
                    {
                        var halfValue = short.MaxValue / 2;
                        var normal = new Vector3(
                                halfValue - BitConverter.ToInt16(v.Unknown0, 0),
                                halfValue - BitConverter.ToInt16(v.Unknown0, 2),
                                BitConverter.ToInt16(v.Unknown0, 4) - halfValue);
                        normal.Normalize();
                        return new VertexPositionColorNormalTexture(
                            new Vector3(-v.Origin.X, v.Origin.Y, v.Origin.Z),
                            new Color(),
                            normal,
                            new Vector2(v.Uv.X, v.Uv.Y));
                    }).ToArray();

                for (int i = 0; i < data.Vertices.Length; ++i)
                {
                    var vertex = vertices[i];
                    vertex.Color = new Color(
                    (byte)(data.Mask[i * 2]),
                    (byte)(data.Mask[i * 2 + 1]),
                    (byte)(0));
                    vertices[i] = vertex;
                }

                foreach (var vertex in vertices)
                {
                    yMax = Math.Max(yMax, vertex.Position.Y);
                    yMin = Math.Min(yMin, vertex.Position.Y);
                }

                var vertexBuffer = new VertexBuffer(graphicsDevice,
                    VertexPositionColorNormalTexture.VertexDeclaration,
                    vertices.Length, BufferUsage.WriteOnly);
                vertexBuffer.SetData(vertices);

                var indices = data.Indices.ToArray();
                Array.Reverse(indices);
                var indexBuffer = new IndexBuffer(graphicsDevice,
                    IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
                indexBuffer.SetData(indices);

                var material = _content.Load<Effect>("Effects/BasicShader");
                var meshObject = new Mesh(material, vertexBuffer, indexBuffer);
                _scene.Meshes.Add(meshObject);
            }

            _scene.Camera.Target = new Vector3(0, (yMax + yMin) / 2, 0);
        }

        public void ExportModel(string path, string format)
        {
            foreach (var mesh in tmmDataFiles)
            {
                var success = TmmAssimp.WriteToFile(tmmFile, mesh, path, format);
            }
        }

        public void ExportModelDebug(string path)
        {
            foreach (var mesh in tmmDataFiles)
            {
                using MemoryStream stream = new();
                using BinaryWriter writer = new(stream);
                mesh.Encode(writer);

                var data = stream.ToArray();
                File.WriteAllBytes(path, data);
            }
        }
    }
}
