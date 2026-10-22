using System;
using System.Collections.Generic;
using UnityEngine;

namespace CampaignDemo
{
    // A display-only mask. Later polygons replace earlier ones, including uncoloured terrain.
    [DisallowMultipleComponent]
    public sealed class TerrainMarchOverlay : MonoBehaviour
    {
        public FreeMarchMapRuntime map;
        public MouseModeStateMachine modes;
        [Range(0, 1)] public float opacity = 0.28f;
        [Range(128, 8192)] public int resolution = 4096;
        public bool terrainVisible;
        public void ToggleTerrain(){terrainVisible=!terrainVisible;Refresh();}
        void Update()
        {
            var events=UnityEngine.EventSystems.EventSystem.current;
            if(events!=null&&events.currentSelectedGameObject!=null&&events.currentSelectedGameObject.GetComponent<UnityEngine.UI.InputField>()!=null)return;
            if(Input.GetKeyDown(KeyCode.R)&&!LocalPrototypeOrders.EnabledFor(this))ToggleTerrain();
        }
        SpriteRenderer display;
        Texture2D texture;
        Sprite sprite;
        FreeMarchMapDefinition cachedDefinition;
        int cachedRevision = -1, cachedResolution;
        float cachedOpacity = -1;

        void LateUpdate() { Refresh(); }

        public void Refresh()
        {
            bool visible = terrainVisible && map != null && map.definition != null;
            if (!visible)
            {
                if (display != null) display.enabled = false;
                return;
            }
            var data = map.definition;
            if (display == null || cachedDefinition != data || cachedRevision != data.Revision ||
                cachedResolution != resolution || cachedOpacity != opacity)
                Rebuild(data);
            if (display != null) display.enabled = texture != null;
        }

        static Color32 Colour(TerrainKind kind, byte alpha)
        {
            switch (kind)
            {
                case TerrainKind.HighMountain: return new Color32(172, 77, 230, alpha);
                case TerrainKind.Mountain: return new Color32(242, 145, 51, alpha);
                case TerrainKind.Grass: return new Color32(105, 165, 95, (byte)Mathf.Max(alpha,115));
                case TerrainKind.Forest: return new Color32(35, 112, 48, alpha);
                case TerrainKind.Blocked: return new Color32(35, 35, 35, (byte)Mathf.Max(alpha,166));
                default: return new Color32(0, 0, 0, 0);
            }
        }

        void Rebuild(FreeMarchMapDefinition data)
        {
            ReleaseImage();
            cachedDefinition = data; cachedRevision = data.Revision;
            cachedResolution = resolution; cachedOpacity = opacity;
            var bounds = data.bounds;
            if (bounds.width <= 0 || bounds.height <= 0) return;
            int longest = Mathf.Clamp(resolution, 128, 8192);
            int width = Mathf.Max(1, Mathf.RoundToInt(longest * bounds.width / Mathf.Max(bounds.width, bounds.height)));
            int height = Mathf.Max(1, Mathf.RoundToInt(longest * bounds.height / Mathf.Max(bounds.width, bounds.height)));
            byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(opacity) * 255);
            var pixels = new Color32[width * height];
            var baseColour = Colour(data.defaultTerrain, alpha);
            if (baseColour.a != 0) for (int i = 0; i < pixels.Length; i++) pixels[i] = baseColour;
            var crossings = new List<float>();
            foreach (var region in data.regions ?? new FreeMarchMapDefinition.Region[0])
            {
                if (region == null || region.polygon == null || region.polygon.Length < 3) continue;
                var polygon = region.polygon;
                float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
                foreach (var point in polygon) { minY = Mathf.Min(minY, point.y); maxY = Mathf.Max(maxY, point.y); }
                int firstY = Mathf.Clamp(Mathf.CeilToInt((minY - bounds.yMin) / bounds.height * height - .5f), 0, height);
                int lastY = Mathf.Clamp(Mathf.CeilToInt((maxY - bounds.yMin) / bounds.height * height - .5f), 0, height);
                var colour = Colour(region.terrain, alpha);
                for (int y = firstY; y < lastY; y++)
                {
                    float localY = bounds.yMin + (y + .5f) / height * bounds.height;
                    crossings.Clear();
                    for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                    {
                        var a = polygon[j]; var b = polygon[i];
                        if ((a.y > localY) != (b.y > localY))
                            crossings.Add(a.x + (localY - a.y) * (b.x - a.x) / (b.y - a.y));
                    }
                    crossings.Sort();
                    for (int i = 0; i + 1 < crossings.Count; i += 2)
                    {
                        int left = Mathf.Clamp(Mathf.CeilToInt((crossings[i] - bounds.xMin) / bounds.width * width - .5f), 0, width);
                        int right = Mathf.Clamp(Mathf.CeilToInt((crossings[i + 1] - bounds.xMin) / bounds.width * width - .5f), 0, width);
                        for (int x = left; x < right; x++) pixels[y * width + x] = colour;
                    }
                }
            }
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "March terrain colours"; texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp; texture.SetPixels32(pixels); texture.Apply(false, false);
            sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            if (display == null)
            {
                var child = new GameObject("March terrain overlay");
                child.layer = map.gameObject.layer;
                display = child.AddComponent<SpriteRenderer>();
            }
            display.transform.SetParent(map.transform, false);
            display.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, 0);
            display.transform.localRotation = Quaternion.identity;
            display.transform.localScale = new Vector3(bounds.width / sprite.bounds.size.x, bounds.height / sprite.bounds.size.y, 1);
            display.sprite = sprite; display.sortingOrder = -9;
        }

        void ReleaseImage()
        {
            if (display != null) display.sprite = null;
            Dispose(sprite); Dispose(texture); sprite = null; texture = null;
        }
        static void Dispose(UnityEngine.Object value)
        { if (value == null) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        void OnDisable() { if (display != null) display.enabled = false; }
        void OnDestroy() { ReleaseImage(); if (display != null) Dispose(display.gameObject); }
    }
}



