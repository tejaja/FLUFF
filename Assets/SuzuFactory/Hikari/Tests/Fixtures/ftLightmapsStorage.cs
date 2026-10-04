using System.Collections.Generic;
using UnityEngine;

namespace SuzuFactory.Hikari.Tests.Fixtures
{
    public sealed class ftLightmapsStorage : MonoBehaviour
    {
        public List<Texture2D> maps = new List<Texture2D>();
        public List<Texture2D> dirMaps = new List<Texture2D>();
        public List<Renderer> bakedRenderers = new List<Renderer>();
        public List<int> bakedIDs = new List<int>();
        public List<Vector4> bakedScaleOffset = new List<Vector4>();
        public int[] idremap = System.Array.Empty<int>();
    }
}
