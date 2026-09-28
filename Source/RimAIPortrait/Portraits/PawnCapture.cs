using System;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Captures the current pawn portrait for generation requests and previews.</summary>
    public static class PawnCapture
    {
        public static byte[] LastCapture { get; private set; }
        public static string LastPawnThingId { get; private set; }

        public static byte[] Capture(Pawn pawn)
        {
            RenderTexture rt = null;
            try
            {
                MethodInfo method = typeof(PortraitsCache).GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "Get" && m.ReturnType == typeof(RenderTexture));
                if (method == null) { Log.Error("[Rim AI Expressive Portraits] PortraitsCache.Get was not found."); return null; }
                var ps = method.GetParameters(); var args = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    Type t = ps[i].ParameterType;
                    args[i] = t == typeof(Pawn) ? pawn : t == typeof(Vector2) ? new Vector2(512, 512) : t == typeof(Rot4) ? Rot4.South : t == typeof(Vector3) ? Vector3.zero : t == typeof(float) ? 1f : t == typeof(bool) ? true : ps[i].HasDefaultValue ? ps[i].DefaultValue : t.IsValueType ? Activator.CreateInstance(t) : null;
                }
                rt = method.Invoke(null, args) as RenderTexture; if (rt == null) { Log.Error("[Rim AI Expressive Portraits] PortraitsCache returned no texture."); return null; }
                var old = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = old;
                byte[] png = tex.EncodeToPNG(); UnityEngine.Object.Destroy(tex);
                LastCapture = png; LastPawnThingId = pawn.ThingID;
                return png;
            }
            catch (Exception e) { Log.Error("[Rim AI Expressive Portraits] Pawn capture failed: " + e); return null; }
        }
    }
}
