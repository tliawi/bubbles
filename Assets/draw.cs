//version 001

using UnityEngine;
using System.Collections;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class draw : MonoBehaviour {

	private void OnDrawGizmos () {

		Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
		
		if (mesh.vertices == null) {
			return;
		}
		Gizmos.color = Color.yellow;
		for (int i = 0; i < mesh.vertices.Length; i++) {
			Gizmos.DrawSphere(mesh.vertices[i], 0.1f);
		}
		//Debug.Log ("onDrawGizmos "+mesh.vertices.Length+": "+mesh.vertices[0].x+" "+mesh.vertices[0].y+" "+mesh.vertices[0].z);
	}

}
