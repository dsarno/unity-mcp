using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace MCPForUnity.Editor.Helpers
{
    public static class MaterialOps
    {
        /// <summary>
        /// Tries to set a shader property on a material based on a JToken value.
        /// Handles Colors, Vectors, Floats, Ints, Booleans, and Textures.
        /// </summary>
        public static bool TrySetShaderProperty(Material material, string propertyName, JToken value, JsonSerializer serializer)
        {
            if (material == null || string.IsNullOrEmpty(propertyName) || value == null)
                return false;

            // Handle stringified JSON (e.g. "[1,0,0,1]" coming as a string)
            if (value.Type == JTokenType.String)
            {
                string s = value.ToString();
                if (s.TrimStart().StartsWith("[") || s.TrimStart().StartsWith("{"))
                {
                    try
                    {
                        JToken parsed = JToken.Parse(s);
                        // Recurse with the parsed token
                        return TrySetShaderProperty(material, propertyName, parsed, serializer);
                    }
                    catch { /* Not valid JSON, treat as regular string */ }
                }
            }

            // Use the serializer to convert the JToken value first
            if (value is JArray jArray)
            {
                // Try converting to known types that SetColor/SetVector accept
                if (jArray.Count == 4)
                {
                    try { material.SetColor(propertyName, ParseColor(value, serializer)); return true; } catch { }
                    try { Vector4 vec = value.ToObject<Vector4>(serializer); material.SetVector(propertyName, vec); return true; } catch { }
                }
                else if (jArray.Count == 3)
                {
                    try 
                    { 
                        material.SetColor(propertyName, ParseColor(value, serializer)); 
                        return true; 
                    } 
                    catch { }
                }
                else if (jArray.Count == 2)
                {
                    try { Vector2 vec = value.ToObject<Vector2>(serializer); material.SetVector(propertyName, vec); return true; } catch { }
                }
            }
            else if (value.Type == JTokenType.Float || value.Type == JTokenType.Integer)
            {
                try { material.SetFloat(propertyName, value.ToObject<float>(serializer)); return true; } catch { }
            }
            else if (value.Type == JTokenType.Boolean)
            {
                try { material.SetFloat(propertyName, value.ToObject<bool>(serializer) ? 1f : 0f); return true; } catch { }
            }
            else if (value.Type == JTokenType.String)
            {
                // Try converting to Texture using the serializer/converter
                try
                {
                    Texture texture = value.ToObject<Texture>(serializer);
                    if (texture != null)
                    {
                        material.SetTexture(propertyName, texture);
                        return true;
                    }
                }
                catch { }
            }
            
            // If we reached here, maybe it's a texture instruction object?
            if (value.Type == JTokenType.Object)
            {
                 try
                {
                    Texture texture = value.ToObject<Texture>(serializer);
                    if (texture != null)
                    {
                        material.SetTexture(propertyName, texture);
                        return true;
                    }
                }
                catch { }
            }

            Debug.LogWarning(
                $"[MaterialOps] Unsupported or failed conversion for material property '{propertyName}' from value: {value.ToString(Formatting.None)}"
            );
            return false;
        }

        /// <summary>
        /// Helper to parse color from JToken (array or object).
        /// </summary>
        public static Color ParseColor(JToken token, JsonSerializer serializer)
        {
            // Handle stringified JSON
            if (token.Type == JTokenType.String)
            {
                string s = token.ToString();
                if (s.TrimStart().StartsWith("[") || s.TrimStart().StartsWith("{"))
                {
                    try
                    {
                        return ParseColor(JToken.Parse(s), serializer);
                    }
                    catch { }
                }
            }

            // Handle Array [r, g, b, a] or [r, g, b]
            if (token is JArray jArray)
            {
                if (jArray.Count == 4)
                {
                    return new Color(
                        (float)jArray[0],
                        (float)jArray[1],
                        (float)jArray[2],
                        (float)jArray[3]
                    );
                }
                else if (jArray.Count == 3)
                {
                    return new Color(
                        (float)jArray[0],
                        (float)jArray[1],
                        (float)jArray[2],
                        1f
                    );
                }
            }
            
            // Handle Object {r:..., g:..., b:..., a:...} via converter
            return token.ToObject<Color>(serializer);
        }
    }
}
