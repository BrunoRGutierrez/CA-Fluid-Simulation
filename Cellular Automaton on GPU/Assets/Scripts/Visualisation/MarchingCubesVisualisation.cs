﻿//#define REALISTIC

using UnityEngine;

namespace GPUFluid
{
    public enum Type
    {
        CUBES, SIMPLE, TESSELATION, ScreenSpaceFluids
    }

    public enum Shading
    {
        FLAT, GOURAUD, PHONG
    }

    /// <summary>
    /// This class executes a marching cubes algorithm on the data of a CellularAutomaton.
    /// At the moment there are two possible visualisations:
    /// The CUBES visualisation creates a voxelised mesh.
    /// The TRIANGLE visualisation creates a smooth mesh.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MarchingCubesVisualisation : MonoBehaviour
    {
        public Type type;

        public Shading shading;

        //The scale of the visualisation
        public Vector3 scale;

        //The size of the CellularAutomaton
        private GridDimensions dimensions;

        //A compute shader that generates a texture3D out of a cellular automaton
        public ComputeShader texture3DCS;
        private int texture3DCSKernel;
        private RenderTexture texture3D;

        //A compute shader that executes the Marching Cubes algorithm
        private ComputeShader marchingCubesCS;
        private int marchingCubesCSKernel;

        //The material for the mesh, that is generated by the Marching Cubes algorithm
        public Material material;

        private ComputeBuffer mesh;

        //A compute buffer that stores the number of triangles/quads generated by the Marching Cubes algorith
        private ComputeBuffer args;
        private int[] data;

        public void Initialize(GridDimensions dimensions)
        {
            this.dimensions = dimensions;
            if(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB4444))
                texture3D = new RenderTexture(dimensions.x * 16, dimensions.y * 16, 1, RenderTextureFormat.ARGB4444);
            else 
                texture3D = new RenderTexture(dimensions.x * 16, dimensions.y * 16, 1);
            texture3D.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            texture3D.filterMode = FilterMode.Trilinear;
            texture3D.volumeDepth = dimensions.z * 16;
            texture3D.enableRandomWrite = true;
            texture3D.Create();

            string path = "MC";

            switch (type)
            {
                case Type.CUBES: path += "_CUBES"; break;
                case Type.SIMPLE: path += "_"; break;
                case Type.TESSELATION: path += "_TESSELATION_"; break;
                case Type.ScreenSpaceFluids: path = "ScreenSpaceFluids"; break;
            }

            if (!(type.Equals(Type.CUBES)|| type.Equals(Type.ScreenSpaceFluids)))
            {
                switch(shading)
                {
                    case Shading.FLAT: path += "FLAT"; break;
                    case Shading.GOURAUD: path += "GOURAUD"; break;
                    case Shading.PHONG: path += "PHONG"; break;
                }
            }

            material = new Material(Resources.Load<Shader>("Shader/Marching Cubes/" + path));

            material.SetTexture("_MainTex", texture3D);

            InitializeComputeBuffer();
            InitializeShader();
        }

        private void InitializeComputeBuffer()
        {
            args = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
            data = new int[4] { 0, 1, 0, 0 };
            args.SetData(data);

            if (type.Equals(Type.CUBES))
            {
                mesh = new ComputeBuffer((dimensions.x * dimensions.y * dimensions.z) * 4096, 4 * 3 * sizeof(float), ComputeBufferType.Append);
            }
            else if (type.Equals(Type.ScreenSpaceFluids))
            {
                mesh = new ComputeBuffer((dimensions.x * dimensions.y * dimensions.z) * 4096, 4 * sizeof(float), ComputeBufferType.Append);
            }
            else
            {
                if (shading.Equals(Shading.FLAT))
                    mesh = new ComputeBuffer((dimensions.x * dimensions.y * dimensions.z) * 4096, 3 * 3 * sizeof(float), ComputeBufferType.Append);
                else
                    mesh = new ComputeBuffer((dimensions.x * dimensions.y * dimensions.z) * 4096, 6 * 3 * sizeof(float), ComputeBufferType.Append);
            }

            ComputeBuffer.CopyCount(mesh, args, 0);
        }

        private void InitializeShader()
        {
            string path = "MarchingCubes_";

            switch(type)
            {
                case Type.CUBES: path += "CUBES"; break;
                case Type.SIMPLE: path += "SIMPLE"; break;
                case Type.TESSELATION: path += "SIMPLE"; break;
                case Type.ScreenSpaceFluids: path = "ScreenSpaceFluidRendering"; break;
            }

            if(!(type.Equals(Type.CUBES) || type.Equals(Type.ScreenSpaceFluids)))
            {
                if(shading.Equals(Shading.GOURAUD) || shading.Equals(Shading.PHONG))
                {
                    path += "wNORMALS";
                }
            }

            marchingCubesCS = Resources.Load<ComputeShader>("ComputeShader/Marching Cubes/" + path);
            marchingCubesCS.SetInts("size", new int[] { dimensions.x * 16, dimensions.y * 16, dimensions.z * 16 });
            marchingCubesCSKernel = marchingCubesCS.FindKernel("CSMain");

            texture3DCS.SetInts("size", new int[] { dimensions.x * 16, dimensions.y * 16, dimensions.z * 16 });
            texture3DCSKernel = texture3DCS.FindKernel("CSMain");

            material.SetVector("scale", new Vector4(scale.x, scale.y, scale.z, 1));
        }

        /// <summary>
        /// Copy from the Water-basic Script from the standard assets.
        /// Used to render realistic water.
        /// </summary>
        private void RenderRealisticWater()
        {
            Vector4 waveSpeed = material.GetVector("WaveSpeed");
            float waveScale = material.GetFloat("_WaveScale");
            float t = Time.time / 20.0f;

            Vector4 offset4 = waveSpeed * (t * waveScale);
            Vector4 offsetClamped = new Vector4(Mathf.Repeat(offset4.x, 1.0f), Mathf.Repeat(offset4.y, 1.0f),
            Mathf.Repeat(offset4.z, 1.0f), Mathf.Repeat(offset4.w, 1.0f));
            material.SetVector("_WaveOffset", offsetClamped);
        }

        /// <summary>
        /// Creates a 3D-Texture out of a cellular automaton. The different fluid-types are represented with different color.
        /// </summary>
        /// <param name="cells">The cells of a CellularAutomaton</param>
        private void RenderTexture3D(ComputeBuffer cells)
        {
            texture3DCS.SetBuffer(texture3DCSKernel, "currentGeneration", cells);
            texture3DCS.SetTexture(texture3DCSKernel, "Result", texture3D);
            texture3DCS.Dispatch(texture3DCSKernel, dimensions.x, dimensions.y * 2, dimensions.z * 2);
        }

        /// <summary>
        /// Perfroms the Marching Cubes Algorithm and generates the mesh.
        /// </summary>
        /// <param name="cells">The cells of a CellularAutomaton</param>
        public void Render(ComputeBuffer cells)
        {
            mesh.SetCounterValue(0);
            marchingCubesCS.SetBuffer(marchingCubesCSKernel, "mesh", mesh);
            marchingCubesCS.SetBuffer(marchingCubesCSKernel, "currentGeneration", cells);
            marchingCubesCS.Dispatch(marchingCubesCSKernel, dimensions.x, dimensions.y * 2, dimensions.z * 2);

            RenderTexture3D(cells);

#if REALISTIC
            if(!type.Equals(Type.CUBES))
                RenderRealisticWater();
#endif
        }

        void OnPostRender()
        {
            material.SetPass(0);

            ComputeBuffer.CopyCount(mesh, args, 0);
            material.SetBuffer("mesh", mesh);
            Graphics.DrawProceduralIndirect(MeshTopology.Points, args);
        }

        /// <summary>
        /// Don't forget releasing the buffers.
        /// </summary>
        void OnDisable()
        {
            mesh.Release();
            args.Release();
        }

        public Vector3 getCellSize()
        {
            return new Vector3(scale.x/(dimensions.x * 16), scale.y / (dimensions.y * 16), scale.z / (dimensions.z * 16));
            //return new Vector3((dimensions.x * 16) / scale.x, (dimensions.y * 16) / scale.y, (dimensions.z * 16) / scale.z);
        }
    }
}