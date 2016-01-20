
using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class Grid {

	private static float uvSquareSize = 1024.0f;
	//private static float zoom = 1.0f;

	private static Vector2[,] atlasUVPositions = new Vector2[4, 3]; //node sprites from atlas, three per class

	private static Vector3[] vertices = new Vector3[0]; // mesh vertices of sprite
	private static Vector2[] uv = new Vector2[0]; // atlas uv positions per vector
	private static int[] triangles = new int[0]; //triangle indices into vertices
	
	private static int demandForLinks;

	private static Mesh mesh;

	public static void deallocate(){
		vertices = new Vector3[0];
		uv = new Vector2[0];
		triangles = new int[0];
	}
	
	//bounds the circle centered at centerx, centery with an equilateral triangle
	private static void setAtlas(int uvClass, float basis, int U, int V){

		float centerx = U*basis, centery = V*basis;
		float radius = basis; //radius is 1/3 of height of bounding triangle
		float halfSide = radius * Mathf.Sqrt (3.0f);

		atlasUVPositions [uvClass, 0] = new Vector2 (centerx, centery + 2*radius);
		atlasUVPositions [uvClass, 1] = new Vector2 (centerx + halfSide, centery - radius);
		atlasUVPositions [uvClass, 2] = new Vector2 (centerx - halfSide, centery - radius);

	}

	private static void makeAtlasUVPositions()
	{	float basis = 55.5f/uvSquareSize;
		setAtlas (0, basis, 1, 17); //animal
		setAtlas (1, basis, 17, 13); //vegetable
		setAtlas (2, basis, 9, 1); //aura was 3,3

		//link
		atlasUVPositions [3, 0] = new Vector2 (955.5f/uvSquareSize, 520.0f/uvSquareSize);
		atlasUVPositions [3, 1] = new Vector2 (980.5f/uvSquareSize, 24.0f/uvSquareSize);
		atlasUVPositions [3, 2] = new Vector2 (930.5f/uvSquareSize, 24.0f/uvSquareSize);
	}

	//given vix, an index into the vertices, triangles and uv arrays, plants a triangle of indices and their uv sprite mappings
	private static int makeTriangle( Vector3 a, Vector3 b, Vector3 c, int uvClass, int vix){

		vertices[vix] = a; //a*zoom;
		triangles[vix] = vix;
		uv[vix++] = atlasUVPositions[uvClass,0];

		vertices[vix] = b; //b*zoom;
		triangles[vix] = vix;
		uv[vix++] = atlasUVPositions[uvClass,1];

		vertices[vix] = c; //c*zoom;
		triangles[vix] = vix;
		uv[vix++] = atlasUVPositions[uvClass,2];

		return vix;
	}

//	private static int makeTriangle( Bub.Node a, Bub.Node b, Bub.Node c, int uvClass, int vix){
//		return makeTriangle( new Vector3(a.x,a.y), new Vector3(b.x,b.y), new Vector3(c.x,c.y), uvClass, vix);
//	}

	private static bool computeVerticesTrianglesUVs()
	{	// order clockwise
		float a0, a1, a2, x0,y0, x1,y1, x2,y2, diameter;
		int uvClass;
		int vix = 0; //vix index into vertices, triangles and uv arrays
		// you can reuse a vertex in multiple triangles, but only if it uses the same UV coords in all--
		// if that vertex is going to carry a different UV coord, it needs to be duplicated in vertices
		Bub.Node anode, targetNode;

		//do this with Mathf.PI/Random.value to make strongly textured aura's or bubbles sparkle a bit
		a0 = 0.0f;
		a1 = a0 - 2 * Mathf.PI / 3;
		a2 = a0 + 2 * Mathf.PI / 3;
		
		x0 = Mathf.Cos (a0); y0 = Mathf.Sin (a0);
		x1 = Mathf.Cos (a1); y1 = Mathf.Sin (a1);
		x2 = Mathf.Cos (a2); y2 = Mathf.Sin (a2);

		//auras (uvClass 2) first, so they're overwritten?
		for (int i = 0; i < Engine.nodes.Count; i++) {

			anode = Engine.nodes[i];

			diameter = 2*anode.radius*Mathf.Pow(anode.oomph/anode.maxOomph, 0.6f);

			vix = makeTriangle(
				new Vector3 (anode.x+x0*diameter, anode.y+y0*diameter),
				new Vector3 (anode.x+x1*diameter, anode.y+y1*diameter),
				new Vector3 (anode.x+x2*diameter, anode.y+y2*diameter),
				2, vix);
		}

		//bubbles

		for (int i = 0; i < Engine.nodes.Count; i++) {

			anode = Engine.nodes[i];
			uvClass = anode.testDna(CScommon.vegetableBit) ? 0:1;
			diameter = 2*anode.radius;

			vix = makeTriangle(
				new Vector3 (anode.x+x0*diameter, anode.y+y0*diameter),
				new Vector3 (anode.x+x1*diameter, anode.y+y1*diameter),
				new Vector3 (anode.x+x2*diameter, anode.y+y2*diameter),
				uvClass, vix);

		}

		//note at this point vix = Engine.nodes.Count*6 -- 3 for each aura, 3 for each bubble.
		//Now for the (variable number of) links

		uvClass = 3;
		float dist, dx, dy, r;

		for (int i = 0; i<Engine.nodes.Count; i++) { //should display bones too
			anode = Engine.nodes[i];
			for (int j = 0; j<anode.rules.Count; j++) {
				for (int k = 0; k<anode.rules[j].musclesCount; k++){
					if (anode.rules[j].muscles(k).notCut){
						targetNode = anode.rules[j].muscles(k).target;
						dist = anode.distance (targetNode);
						r = 3.0f*Mathf.Sqrt(anode.rules[j].muscles(k).strength());//the constant factor is purely display taste
						dx = r*(targetNode.y - anode.y)/dist;
						dy = r*(targetNode.x - anode.x)/dist;

						vix = makeTriangle(
							new Vector3(targetNode.x, targetNode.y),
							new Vector3(anode.x + dx, anode.y - dy), 
							new Vector3(anode.x - dx, anode.y + dy),
							uvClass, vix);
					}
				}
			}
			if (vix == vertices.Length) return true; //not enough vertices etc allocated
		}

		//fake up unused vertices and triangles???

		return false; //had adequate number of vertices etc allocated
	}
	/* meh...

	//find the index into the vertices array of the 
	//vertex of the triangle about source, that is closest to the target.
	// (Actually you'll be interested in the other two vertices, so this is the one you'll reject.)
	//For a given bubble source, firstVertexIndex is bubblesStart + 3*source.id, 
	// it is the first of the vertices about the source bubble.
	// In other words, this will return either firstVertexIndex, or firstVertexIndex + 1, or firstVertexIndex + 2.
	private int nearest(int firstVertexIndex, Bub.Node target) {

		//the triangle beginning at firstVertexIndex
		Vector3 v0 = vertices[firstVertexIndex], 
			v1 = vertices[firstVertexIndex + 1], 
			v2 = vertices[firstVertexIndex + 2];

		//distances squared from the triangle vertices to the target
		float d0 = (target.x - v0.x)*(target.x - v0.x) + (target.y - v0.y)*(target.y - v0.y),
			d1 = (target.x - v1.x)*(target.x - v1.x) + (target.y - v1.y)*(target.y - v1.y),
			d2 = (target.x - v2.x)*(target.x - v2.x) + (target.y - v2.y)*(target.y - v2.y);

		//find the least
		if (d0 > d1) {
			if (d1 > d2) return firstVertexIndex+2; //d0,d1
			else return firstVertexIndex+1; //do,d2
		} else {
			if (d0 > d2) return firstVertexIndex+2; //d1,d0
			else return firstVertexIndex; //d1,d2
		}
	}

	targetNode = anode.links[j].target;
	avertex = new Vector3(targetNode.x, targetNode.y); // plant a new vertex at center of target
	
	//triangle starts with new vertex, and includes the two existing FURTHER vertices of the triangle about the source
	closestVertexi = nearest (firstVertx, targetNode);
	// 0 > 1 > 2 > 0, to preserve clockwise order so that face of triangle will display
	if (closestVertexi == firstVertx){
		vix = makeTriangle(avertex, vertices[firstVertx+1], vertices[firstVertx+2], uvClass, vix);
	} else if (closestVertexi == firstVertx + 1){
		vix = makeTriangle(avertex, vertices[firstVertx+2], vertices[firstVertx], uvClass, vix);
	} else if (closestVertexi == firstVertx + 2){
		vix = makeTriangle(avertex, vertices[firstVertx], vertices[firstVertx+1], uvClass, vix);
	} else Debug.Log ("error in nearest");

*/

	public static void initialize () { //called from bubbleServer.Awake(), after it has called Bots.initialize()

		makeAtlasUVPositions ();
		
		demandForLinks = Engine.nodes.Count*3; //initial provision for an average of 3 links per bubble
		//demandForLinks will adapt if number of links is more

	}

	public static void display() {

		//adaptively allocate vertices, triangles and uv
		if (vertices.Length < 6*Engine.nodes.Count + 3*demandForLinks){ //3 for bubble, 3 for aura, 3 per link
			vertices = new Vector3[6*Engine.nodes.Count + 3*demandForLinks];
			triangles = new int[vertices.Length];
			uv = new Vector2[vertices.Length];
		}

		if (computeVerticesTrianglesUVs ()) demandForLinks = (int)Mathf.Floor (demandForLinks*1.1f); //next update will reallocate

		mesh = GameObject.Find ("MeshObject").GetComponent<MeshFilter>().sharedMesh;
		mesh.Clear();
		//MeshFilter mshFltr = GameObject.Find ("MeshObject").GetComponent<MeshFilter>();
		//mshFltr.mesh = mesh = new Mesh();

		mesh.name = "Procedural Grid";
		
		mesh.vertices = vertices;
		mesh.uv = uv;
		mesh.triangles = triangles;
		
		mesh.RecalculateNormals();
	}

}
