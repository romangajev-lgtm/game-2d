using UnityEngine;
using UnityEngine.UI;

namespace AIInterrogation
{
    public static class RuntimeMaterialFactory
    {
        private static Material uiMaterial;
        private static Material spriteMaterial;
        private static Material textMaterial;

        public static Material UiMaterial
        {
            get
            {
                if (uiMaterial == null)
                {
                    uiMaterial = CreateMaterial("Shaders/AIInterrogationUI", "UI/Default");
                }

                return uiMaterial;
            }
        }

        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial == null)
                {
                    spriteMaterial = CreateMaterial("Shaders/AIInterrogationSprite", "Sprites/Default");
                }

                return spriteMaterial;
            }
        }

        public static Material TextMaterial
        {
            get
            {
                if (textMaterial == null)
                {
                    textMaterial = CreateMaterial("Shaders/AIInterrogationText", "UI/Default");
                }

                return textMaterial;
            }
        }

        public static void ApplyTo(Graphic graphic)
        {
            if (graphic != null && UiMaterial != null)
            {
                graphic.material = UiMaterial;
            }
        }

        public static void ApplyToText(Text text)
        {
            if (text != null && TextMaterial != null)
            {
                text.material = TextMaterial;
            }
        }

        private static Material CreateMaterial(string resourcesPath, string fallbackShaderName)
        {
            var shader = Resources.Load<Shader>(resourcesPath);
            if (shader == null)
            {
                shader = Shader.Find(fallbackShaderName);
            }

            if (shader == null)
            {
                Debug.LogError($"Shader is missing: {resourcesPath}");
                return null;
            }

            var material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return material;
        }
    }
}
