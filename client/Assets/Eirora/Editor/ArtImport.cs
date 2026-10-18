using UnityEditor;

namespace Eirora.Editor
{
    /// <summary>
    /// Import rules for models made by hand (ADR-0009, docs/design/looks.md): anything in Assets/Eirora/Art/Models is
    /// a vertex-coloured mesh that looks bake into the island, so it must stay readable and needs no materials,
    /// animation or lightmap UVs of its own.
    /// </summary>
    public sealed class ArtImport : AssetPostprocessor
    {
        public const string ModelsFolder = "Assets/Eirora/Art/Models/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelsFolder, System.StringComparison.Ordinal) || assetImporter is not ModelImporter model)
            {
                return;
            }

            model.isReadable = true;
            model.bakeAxisConversion = true;
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.importAnimation = false;
            model.animationType = ModelImporterAnimationType.None;
            model.importCameras = false;
            model.importLights = false;
            model.importBlendShapes = false;
            model.generateSecondaryUV = false;
            model.meshCompression = ModelImporterMeshCompression.Off;
            model.importNormals = ModelImporterNormals.None;
        }
    }
}
