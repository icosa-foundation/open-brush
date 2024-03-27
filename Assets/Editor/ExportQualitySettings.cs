using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public class ExportQualitySettings
{
    [MenuItem("Open Brush/Export Quality Settings to CSV")]
    private static void ExportQualitySettingsToCSV()
    {
        StringBuilder csvContent = new StringBuilder();
        string filePath = Path.Combine(Application.dataPath, "QualitySettings.csv");

        // Add CSV headers
        csvContent.AppendLine("Name,pixelLightCount,antiAliasing,realtimeReflectionProbes,resolutionScalingFixedDPIFactor,vSyncCount,anisotropicFiltering,masterTextureLimit,streamingMipmapsActive,streamingMipmapsMemoryBudget,streamingMipmapsRenderersPerFrame,streamingMipmapsMaxLevelReduction,streamingMipmapsMaxFileIORequests,streamingMipmapsAddAllCameras,softParticles,particleRaycastBudget,billboardsFaceCameraPosition,shadowmaskMode,shadows,shadowResolution,shadowProjection,shadowDistance,shadowNearPlaneOffset,shadowCascades,skinWeights,asyncUploadTimeSlice,asyncUploadBufferSize,asyncUploadPersistentBuffer,lodBias,maximumLODLevel,skinWeights");

        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, applyExpensiveChanges: true);
            string line = string.Format("\"{0}\",{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25},{26},{27},{28},{29},{30}",
                QualitySettings.names[i],
                QualitySettings.pixelLightCount,
                QualitySettings.antiAliasing,
                QualitySettings.realtimeReflectionProbes,
                QualitySettings.resolutionScalingFixedDPIFactor,
                QualitySettings.vSyncCount,

                QualitySettings.anisotropicFiltering,
                QualitySettings.masterTextureLimit,
                QualitySettings.streamingMipmapsActive,
                QualitySettings.streamingMipmapsMemoryBudget,
                QualitySettings.streamingMipmapsRenderersPerFrame,
                QualitySettings.streamingMipmapsMaxLevelReduction,
                QualitySettings.streamingMipmapsMaxFileIORequests,
                QualitySettings.streamingMipmapsAddAllCameras,

                QualitySettings.softParticles,
                QualitySettings.particleRaycastBudget,

                QualitySettings.billboardsFaceCameraPosition,

                QualitySettings.shadowmaskMode,
                QualitySettings.shadows,
                QualitySettings.shadowResolution,
                QualitySettings.shadowProjection,
                QualitySettings.shadowDistance,
                QualitySettings.shadowNearPlaneOffset,
                QualitySettings.shadowCascades,

                QualitySettings.skinWeights,

                QualitySettings.asyncUploadTimeSlice,
                QualitySettings.asyncUploadBufferSize,
                QualitySettings.asyncUploadPersistentBuffer,

                QualitySettings.lodBias,
                QualitySettings.maximumLODLevel,

                QualitySettings.skinWeights
            );

            csvContent.AppendLine(line);
        }

        File.WriteAllText(filePath, csvContent.ToString());
        Debug.Log("Quality settings dumped to " + filePath);
    }
}
