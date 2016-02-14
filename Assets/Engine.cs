// copyright 2015-2016 John Fairfield

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics; //for stopwatch

namespace Bubbles{
public class Engine {

	private static int gInterval = 2;

	//list of all nodes in universe
	public static readonly List<Node> nodes = new List<Node>();

	public static readonly List<Node> scheduledOrgRelocations = new List<Node>();
	
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
		{	Node.resetGravity();
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

	private static void checknXnY(string msg){
		for (int i=0; i < nodes.Count;i++){
			bubbleServer.checkVals (nodes[i].nx,nodes[i].ny,msg+":"+nodes[i].id+":nXnY");
		}
	}

	private static void checkXY(string msg){
		for (int i=0; i < nodes.Count;i++){

		
			bubbleServer.checkVals(nodes[i].x,nodes[i].y,msg+":"+nodes[i].id+":XY");
		
			bubbleServer.checkVals(nodes[i].nx,nodes[i].ny,msg+":"+nodes[i].id+":NXNY");
			
			UnityEngine.Debug.Assert (nodes[i].x == nodes[i].nx && nodes[i].y == nodes[i].ny, msg+":"+nodes[i].id+": x!=nx or y!=ny");

		}
	}


	private static long preVoronoi, inVoronoi, postVoronoi;
	static void printVoronoiTimes(){
		double sum = preVoronoi + inVoronoi + postVoronoi;
		UnityEngine.Debug.Log(sum/(1000*tickCounter)+" ms:"+preVoronoi/sum+" "+inVoronoi/sum+" "+postVoronoi/sum);
	}

	private static void makeNeighbors(){
		
		Stopwatch sw = new Stopwatch();
		sw.Start();

		float x, y, maxx = nodes[0].x, maxy = nodes[0].y, minx=nodes[0].x, miny=nodes[0].y;

		for (int i=0; i < nodes.Count;i++){

			x = nodes[i].x; y = nodes[i].y;
			if(float.IsNaN(x) || float.IsNaN (y) || float.IsInfinity(x) || float.IsInfinity (y)){
				bubbleServer.debugDisplay("ERROR: makeNeighbors Nan or Inf "+i);
				nodes[i].x = nodes[i].y = 33; //something!
			}
			if (maxx < x) maxx = x;
			if (minx > x) minx = x;
			if (maxy < y) maxy = y;
			if (miny > y) miny = y;
		}

		preVoronoi += sw.ElapsedTicks;

		sw.Reset(); sw.Start();

		//if (voronoi != null) voronoi.Dispose(); slowed things down! pools not efficiently implemented

		Delaunay.Voronoi voronoi = new Delaunay.Voronoi (Engine.nodes, new Rect(minx-1,miny-1, 2+maxx-minx, 2+maxy-miny));
		inVoronoi += sw.ElapsedTicks;
		sw.Reset(); sw.Start();
	
		postVoronoi += sw.ElapsedTicks;
		sw.Reset();
	}
	
	private static void tryToEatNeighbors()
	{	for (int i=0; i<nodes.Count; i++) if (nodes[i].isEater ()) for (int j=0;j<nodes[i].site.neighborsCount(); j++) 
		nodes[i].tryEat(nodes[i].site.neighbors(j));
	}

	private static void doScheduledRelocations(){
		foreach (var node in scheduledOrgRelocations) node.randomRelocateOrganism();
		scheduledOrgRelocations.Clear ();
	}

	static long makeNeighborsTime, tryToEatNeighborsTime, doAllRulesTime, activateAllTime, updatePositionsTime, relocationsTime, photoOomphTime;
	static void printTimes(){
		double sumTimes = makeNeighborsTime+tryToEatNeighborsTime+doAllRulesTime+activateAllTime+updatePositionsTime+relocationsTime+photoOomphTime;
		UnityEngine.Debug.Log(sumTimes/(1000*tickCounter)+" ms:"+makeNeighborsTime/sumTimes+" "+tryToEatNeighborsTime/sumTimes+" "+doAllRulesTime/sumTimes+" "+activateAllTime/sumTimes+" "+updatePositionsTime/sumTimes+" "+relocationsTime/sumTimes+" "+photoOomphTime/sumTimes);
	}
		
	public static int tickCounter { get; private set; }

	public static void initialize(){
		tickCounter = 0;
	}

	//called every fixedUpdate
	public static void step(){
		Stopwatch sw = new Stopwatch();

		//x,y == nx, ny
							//checkXY("pre makeNeighbors");
		sw.Start();

		if (tickCounter%5 == 0) makeNeighbors(); //look around, create voronoi neighbor graph.
		makeNeighborsTime += sw.ElapsedTicks;

		//collision detection based on voronoi neighbors
		sw.Reset(); sw.Start();
		tryToEatNeighbors(); //get (or lose) oomph, perhaps schedule forced relocation, but
		//defer relocations, because otherwise would have to recompute voronoi for rules
		tryToEatNeighborsTime += sw.ElapsedTicks;

		sw.Reset(); sw.Start();
		doAllRules();
		doAllRulesTime += sw.ElapsedTicks;

							//checkXY("pre activateAll"); //should be unchanged from pre makeNeighbors
		//nx and ny begin to accumulate change based on muscles and gravity.
		sw.Reset(); sw.Start();
		activateAll();//During and hereafter there's a difference between x, y and nx,ny
		activateAllTime += sw.ElapsedTicks;

							//checknXnY("post activateAll");
		//final adjustment to nx ny based on gravity.
		sw.Reset(); sw.Start();
		updatePositions(); // Update x,y to == nx,ny
		updatePositionsTime += sw.ElapsedTicks;

							//checkXY("post updatePositions");
		sw.Reset(); sw.Start();
		doScheduledRelocations(); //relocations scheduled by eating.
		relocationsTime += sw.ElapsedTicks;

							//checkXY("post relocations");

		//Prepare the future
		sw.Reset(); sw.Start();
		photosynthesize(); //generate oomph
		shareOomph();
		photoOomphTime += sw.ElapsedTicks;

		tickCounter++;

		if (UnityEngine.Debug.isDebugBuild && tickCounter%1000 == 0) { printTimes(); printVoronoiTimes();}
	}
	

	}}
