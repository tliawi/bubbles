﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Engine {

	private static int gInterval = 2;
	
	public static readonly List<Bub.Node> nodes = new List<Bub.Node>();
	
	public static int closestNodeId(float x, float y){
		float closestD2 = 40000000;
		int closestId = -1;
		for (int i = 0; i < nodes.Count; i++){
			float d2 = (x-nodes[i].x)*(x-nodes[i].x)+(y-nodes[i].y)*(y-nodes[i].y);
			if (d2 < closestD2) {closestD2 = d2; closestId = i;}
		}
		return closestId;
	}

	
	public static void deallocate(){
		nodes.Clear (); //all the nodes, their links, their rules, their rules' closures of node references...
	}

	
	public static int tickCounter { get; private set; }
	
	private static void doAllRules(){
		for (int i=0; i < nodes.Count; i++) nodes[i].rulesAccion();
	}

	public static void activateAll(){
		for (int i=0; i < nodes.Count; i++) nodes[i].rulesMuscleAction();
		//must be done after muscle actions all done?? why??
		for (int i=0; i < nodes.Count; i++) nodes[i].activateBones();
	}
	
	//when this is called from tick, the previous calculations have developed a 
	//hallucination of where each node ought to move to, in node.x and node.y.
	//updatePosition does a final adustment of the hallucination, 
	//to curve stuff back towards the origin, so that stuff doesn't move out forever into space.
	//Then it adopts the hallucination as the new px,py position of the node.
	private static void updatePositions()
	{	
		if ( tickCounter%gInterval == 0)
		{	Bub.resetGravity();
			gInterval = Random.Range(500,1000); //even if large next interval could be very short, depending on tickCounter
		}
		
		for (int i=0; i < nodes.Count; i++) nodes[i].updatePosition();
		
	}
	
	
	private static void photosynthesize()
	{	for (int i=0; i< nodes.Count; i++) nodes[i].photosynthesis(); // oomph gain stored in node.oomph
	}
	
	private static void shareOomph()
	{	for (int i=0; i< nodes.Count; i++) nodes[i].shareOomph();
	}
	
	private static void makeNeighbors(){
		float x, y, maxx = nodes[0].x, maxy = nodes[0].y, minx=nodes[0].x, miny=nodes[0].y;
		List<Vector2> coords = new List<Vector2>();
		List<uint>ids = new List<uint>();
		for (int i=0; i < nodes.Count;i++){
			ids.Add ((uint)i); //i = nodes[i].id
			x = nodes[i].x; y = nodes[i].y;
			if ( float.IsNaN (x) || float.IsNaN (y)) Debug.Log("makeNeighbors Nan x:"+float.IsNaN (x) + " y:"+float.IsNaN(y));
			if (maxx < x) maxx = x;
			if (minx > x) minx = x;
			if (maxy < y) maxy = y;
			if (miny > y) miny = y;
			coords.Add (new Vector2(x,y));
		}
		//use "color" (second parm of Voronoi constructor) to map from voronoi sites to my nodes
		Delaunay.Voronoi v = new Delaunay.Voronoi (coords, ids, new Rect(minx-1,miny-1, 2+maxx-minx, 2+maxy-miny));

		for (int i=0; i < nodes.Count;i++){
			Bub.Node n = nodes[i];
			if (n.id != i){
				Debug.Log ("i:"+i+" != n.id:"+n.id);
			}
			n.neighbors.Clear ();
			List<int> neighborIDs = v.NeighborIDs (new Vector2(n.x, n.y));
			if (neighborIDs.Count == 0){
				Debug.Log ("no neighborIDs");
				continue;
			}
			
			if (neighborIDs[0] != n.id){ // a very rare but possible condition caused by loss of numerical distinction due to the many independent influences (muscle, bone, gravity) on nodes
				Debug.Log ("nodes["+i+"] and nodes["+neighborIDs[0]+"] have identical coordinates. x:"+n.x+" y:"+n.y+" x:"+nodes[neighborIDs[0]].x+" y:"+nodes[neighborIDs[0]].y);
				//so what to do? We have to deal with this situation. 
				//Brute force arbitrarily add distinction so that it doesn't repeat itself
				n.x += Random.Range (-0.001f, 0.001f);
				n.y += Random.Range (-0.001f, 0.001f);
			}
			
			for (int j=  1  ;j<neighborIDs.Count; j++){	
				n.neighbors.Add (nodes[neighborIDs[j]]);
			}
		}
		
	}
	
	private static void tryToEatNeighbors()
	{	for (int i=0; i<nodes.Count; i++) if (nodes[i].isEater ()) for (int j=0;j<nodes[i].neighbors.Count; j++) 
		nodes[i].tryEat(nodes[i].neighbors[j]);
	}

	
	//called every fixedUpdate
	public static void step(){
		
		tickCounter++;
		
		doAllRules();

		activateAll();//During and hereafter there's a difference between x, y and nx,ny
		
		updatePositions(); //update x, y to nx,ny: hereafter no difference between them
		
		//trackEnergy();
		
		//Prepare the future
		photosynthesize(); //generate oomph
		
		shareOomph();
		
		makeNeighbors(); //look around
		
		tryToEatNeighbors(); //get (or lose) oomph, perhaps destroy some bots
		//doSmarts(); //change tactics, shift node burden, muscle link parameters etc
		
		
		
	}
	

}