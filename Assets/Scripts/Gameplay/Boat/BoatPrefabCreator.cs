using UnityEngine;

namespace Gameplay.Boat
{
    public static class BoatPrefabCreator
    {
        public static GameObject CreateFallbackBoat()
        {
            // Create boat hull
            var boat = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            boat.name = "Boat_Fallback";
            boat.transform.localScale = new Vector3(0.5f, 0.2f, 1f);
            
            // Remove collider (we'll handle collision separately)
            Object.DestroyImmediate(boat.GetComponent<CapsuleCollider>());
            
            // Set boat color
            var renderer = boat.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.8f, 0.6f, 0.4f); // Brownish
            }
            
            // Create sail
            var sail = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sail.name = "Sail";
            sail.transform.SetParent(boat.transform);
            sail.transform.localPosition = new Vector3(0, 0.8f, 0);
            sail.transform.localScale = new Vector3(0.8f, 1.2f, 1f);
            
            var sailRenderer = sail.GetComponent<Renderer>();
            if (sailRenderer != null)
            {
                sailRenderer.material.color = Color.white;
            }
            
            // Remove sail collider
            Object.DestroyImmediate(sail.GetComponent<MeshCollider>());
            
            return boat;
        }
    }
}