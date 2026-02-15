using UnityEngine;
using System.Text;

public class BlendShapeDebugger : MonoBehaviour
{
    [Tooltip("Drag the character's head or body mesh here")]
    public SkinnedMeshRenderer targetMesh;

    void Start()
    {
        if (targetMesh == null)
        {
            Debug.LogError("No SkinnedMeshRenderer assigned to the debugger!");
            return;
        }

        Mesh mesh = targetMesh.sharedMesh;
        int count = mesh.blendShapeCount;

        Debug.Log($"<color=cyan><b>Found {count} BlendShapes on {targetMesh.name}:</b></color>");

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("All BlendShapes (Copy-Paste Friendly):");

        for (int i = 0; i < count; i++)
        {
            string name = mesh.GetBlendShapeName(i);
            // Formatted as "Name", for easy array/list usage
            sb.AppendLine($"\"{name}\",");
        }
        
        Debug.Log(sb.ToString());
        Debug.Log("<color=green><b>End of BlendShape List</b></color>");
    }
}