//Bubbles world physics engine

using UnityEngine;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;

public class Bub {
	
	public static readonly float minRadius = 0.0001f;  //bottom of fractal possibilities, don't want to push float representation
	public static readonly float minPosValue = 0.000001f;
	//physics fundamental tuning constants
	public static readonly float minBurdenMultiplier = 0.1f; //governs how more efficient use of shiftBurden is, than solo movement. If 1, inchworms more comperable to solos.

	// photoyield should be slow enough that inchworms that don't eat should be slow. 
	// That makes solos that don't eat minBurdenMuliplier/2 as fast, since they can't shiftBurden, and there's not two photosynthesizers
	public static float photoYield = 0.08f;
	public static float baseMetabolicRate = 0.0035f;
	public static float worldRadius = 400f;

	private static float gGravity;//see resetGravity. OccaSsional large perterbations in gravity can have a pleasing and disruptive effect on circling
	private static Vector2 gCG = new Vector2(0.0f,0.0f); //Center of world is 0,0, but CG, Center of Gravity, can be moved about a bit to give a nice effect
	
	public static void resetGravity(){
		float norm = -1.7f*Mathf.Log(worldRadius); //larger neg value makes gravity weaker
		gGravity = Mathf.Exp(Random.Range(norm-norm/4, norm+norm/4));
		gCG.x = 0.2f*worldRadius*(Random.value - 0.5f);
		gCG.y = 0.2f*worldRadius*(Random.value - 0.5f);
	}

	public static void initialize(){
		Bub.resetGravity();
	}

	//old, when photosyn always added photoYield*r^2;
	//(1-0.004)^180 ~ 0.5, so if running links, half life of oomph is 3 60-frame seconds.
	//see spreadsheet "plusTimesVsTimesPlus": The limit (not regarding maxOomph) in 'normal' (strengthbit off) vegetable nodes
	//of the metabolicOutput process (multiplies oomph by (1-baseMetabolicRate), a geometric process) FOLLOWED BY the
	//photosynthetic process (adds photoyield*r^2 to oomph, an arithmetic process)
	//is photoyield*r^2 / baseMetabolicRate, which for a node of radius = 1 is 0.1/0.004 = 25, 
	//and at that limit of 25 you can supply 25*baseMetabolicRate = 0.1 oomph per frame,
	//which will move a burden of 1 0.5*0.1/1 = 0.05 per frame (60/sec), i.e. about 3 of your radius every second, slow


	// aNodes and bNodes are lists of nodes, all of whom must belong to the same trustGroup else the whole operation fails.
		// Some nodes may be in both aNodes and bNodes.
		// 0 <= fraction <=1
		// Attempts to put fraction of the total burden in aNodes, and (1-fraction) thereof in bNodes.
		// Does nothing if either list is empty, or if fraction out of bounds.
		// Will never set any node's burden below its minBurden even if fraction would call for that,
		// choosing in that case to shift as much as possible, but no more. 
		// So it's safe to call with fraction == 0 or 1.
	public static void shiftBurden(float fraction, List<Node> aNodes, List<Node> bNodes){ 
		float leftovers = 0, aPart, bPart;
		
		if (aNodes.Count == 0 || bNodes.Count == 0) return;
		if (fraction < 0 || fraction > 1) return;
		
		Node firstNode = aNodes[0];
		//all must belong to the same trustGroup
		foreach (Node n in aNodes) if (!firstNode.trusts(n)) return; 
		foreach (Node n in bNodes) if (!firstNode.trusts(n)) return;
		
		foreach(Node n in aNodes) {leftovers+= n.burden - n.minBurden; n.burden = n.minBurden ;}
		
		//any node in both aNodes and bNodes already has its minburden, so contributes nothing further to leftovers
		foreach(Node n in bNodes) {leftovers+= n.burden - n.minBurden; n.burden = n.minBurden;}
		
		//what to distribute to each node
		aPart = leftovers*fraction/aNodes.Count;
		bPart = leftovers*(1.0f-fraction)/bNodes.Count;
		
		foreach(Node n in aNodes) n.burden += aPart;
		foreach(Node n in bNodes) n.burden += bPart; // += so any shared node gets both loads
		
	}

	public static float distance2(Node n1, Node n2){
		return (n1.x-n2.x)*(n1.x-n2.x)+(n1.y-n2.y)*(n1.y-n2.y);
	}

	// the efficiency of a link between the two nodes.
	// public, and not a link method, so that bots can determine which potential muscles would be most efficient
	// This is independent of metabolicOutput--the node's ability to supply the link with oomph. 
	// It is wholly a function of link length AND the source's radius--so linkEfficiency is NOT symmetric
	public static float linkEfficiency(Node source, Node target){ 
		return CScommon.efficiency(distance2(source,target),source.radius2);
	}


    private static string getRandomClan()
    {return Random.Range(0,int.MaxValue).ToString();}



	//bitNumber 0 is least significant bit
	public static bool testBit(long dna, int bit)
	{ return ((dna & (1L << bit)) != 0L);}

	public static long setBit(long dna, int bit, bool value)
	{	if (testBit(dna, bit) != value) dna = dna ^ (1L<<bit);
		return dna;
	}


	/////////////////////////////////////////////////////////////////////////////////// 
	
	public class Muscle {
		
		public Bub.Node source { get; protected set; }
		public Bub.Node target;

		public float demand {get; protected set;}
		public bool enabled {get{ return demand > 0;}} //deprecated
		public bool disabled { get {return demand == 0;}} //deprecated
		public Muscle enable(int percent = 100){
			if (percent>=0 && percent <= 300) demand = source.radius2*baseMetabolicRate * percent;//some day * CScommon.testBit(source.dna, CScommon.strengthBit)?10:1; 
			return this;}
		public Muscle disable(){ demand = 0; return this; }

		private bool pulling;
		public bool isPuller() {return pulling;}
		public bool isPusher() { return !pulling;}
		public Muscle makePuller() {pulling = true; return this;}
		public Muscle makePusher() {pulling = false; return this;}
		public Muscle togglePushPull() {pulling = !pulling; return this;}
		public CScommon.LinkType commonType() {return pulling?CScommon.LinkType.puller:CScommon.LinkType.pusher;}
		
		public Muscle( Bub.Node source0, Bub.Node target0) {
			source = source0;
			target = target0;
			
			enable();
			pulling = true;
		}
		
		public float length(){ return source.distance(target); }
		public float length2() { return source.distance2(target);}
		public float relativeLength(){ return source.distance (target)/source.radius; }
		public float efficiency(){ return CScommon.efficiency(source.distance2(target),source.radius2); }
		
		//the oomph that this particular muscle can actually put to work (some is wasted in inefficiency)
		// units: is oomp is d*b
		
		public float strength() { 
			return demand*CScommon.efficiency(length2(),source.radius2);
		}
		
		// friction narrative: the force of a link operates independently on both ends. 
		// A better way of putting it: each end gets half the oomph expended on it. That way, the amount one end moves
		// is independent of how much the other end moves--it all depends on their individual burdens.
		// The unit of oomph is a burden meter
		// So total amount of lengthening or shortening is
		private float oomphToDisplacement(float omp){
			return 0.5f*omp*(1/source.burden + 1/target.burden);
		}
		
		private float displacementToOomph(float disp){
			return 2*disp/(1/source.burden + 1/target.burden);
		}

		//don't want to waste energy on pulling when you've already pulled
		//don't want to pull until nodes have identical x and y
		private float ceasePullDistance() {return 0.95f*source.radius;}

		// must not debit oomph finally until all muscles have partaken, else later muscles would have less oomph than earlier muscles
		public float actionDemand()
		{	if (isPuller() && length() < ceasePullDistance()) return 0;
			return demand;
		}

		// Executes the muscle action given sources's perhaps limited ability to power the muscle
		public void action(float fraction){
			float dx, dy, displacement, effect;

			if ( fraction*demand == 0) return;
			if (length()==0) return;

			dx = target.x - source.x;
			dy = target.y - source.y;

			// Effect on one end is independent of effect on the other.
			// Each experiences the same 'force', they react to it in inverse proportion to their burden.
			// Strength == oomph is change on a unit burden,
			// but in general both ends don't have unit burden, so
			// actual change in distance between nodes is
			displacement = oomphToDisplacement(fraction*demand)*efficiency(); //magnitude of how much this link will lengthen or shorten

			// insure that puller doesn't overshoot zero.
			// Further, ensure that length stays north of an epsilon:
			// if length gets too close to zero, numerical effects can result in node superposition
			// Further, ensure that puller never pulls you closer than you need to eat, to keep nodes from being
			// so confounded that it's difficult for players to distinguish them.
			// Also, Perfect superposition is hard on voronoi, and you can't start pushing on it with any notion of direction of push, etc.
			
			if (isPuller()) displacement = Mathf.Min (length() - ceasePullDistance(), displacement);
			if (displacement < 0) displacement = 0; 

			// give a sign to displacement-- pos if pushing (trying to increase length), neg if pulling
			if (isPuller()) displacement = -displacement; 
			
			if (source.burden <= 0.0f) bubbleServer.debugDisplay ("bad source burden");
			if (target.burden <= 0.0f) bubbleServer.debugDisplay ("bad target burden");
			
			source.pushedMinusPulled += displacement; // accumulate positive if pushed, negative if pulled
			target.pushedMinusPulled += displacement; // gravity question: should this be weighted (as below) by inverse burden?
			
			effect = displacementToOomph(displacement); // effect is signed oomph: displacement of a unit burden. Units: bd == o
			//Debug.Log ("displacement:"+displacement+" effect:"+effect+" dx:"+dx+" length:"+length);
			
			//Units of effect change from gd to g, i.e. express it as a fraction of length, and cut it in half to equally apply it to both ends
			effect = effect/(2*length());
			
			// has full effect on unit burden
			//A smaller burden moves more than a bigger burden.
			source.nx -= dx*effect/source.burden;
			source.ny -= dy*effect/source.burden;
			
			target.nx += dx*effect/target.burden;
			target.ny += dy*effect/target.burden;

		}
	}


	//////////////////////////////////////////////////////////////// bones

	public static float boneStiffness = 0.4f;

	public class Bone{

		public Node source {get; private set;}
		public Node target {get; private set;}
		public float boneLength { get; private set; }

		public Bone (Node source0, Node target0){
			source = source0;
			target = target0;
			boneLength = source.distance(target);
		}

		//only for bone links. Elastically pushes or pulls its source/target pair, to maintain the distance between them at boneLength
		public void boneAction()
		{	float 	dx = target.x - source.x,
			dy = target.y - source.y;
			float dislocation, effect;

			dislocation = boneLength - source.distance(target);
			
			// Effect on one end is independent of effect on the other.
			// Each experiences the same 'force', they react to it in inverse proportion to their burden.
			effect = boneStiffness*dislocation; // - if too long, + if too short.

			//A smaller burden moves more than a bigger burden.
			effect = 0.5f * effect * target.burden /(source.burden + target.burden);

			source.nx -= dx*effect;
			source.ny -= dy*effect;
			
			//target will be moved when target processes this bone's twin
			//			target.nx += dx*effect/target.burden;
			//			target.ny += dy*effect/target.burden;
			
		}
		

	} ////////////////////////////////////////////////////////////////////////////
	
	//////////////////////////////////////////////////////////////////////////// Class Node

	public class Node {
		public int id  { get; private set; } //this nodes' index into the nodes array
		public float x;
		public float y;

		//all these functions of radius, set in setRadius
		public float radius  { get; private set; }
		public float radius2 { get; private set;}
		public float minBurden { get; private set; }
		public float maxOomph { get; private set; }

		public long dna  { get; private set; }
		public float oomph;
		public float burden;

		public List<Bone> bones;
		public List<Node> neighbors;

		public float nx, ny; //hallucinated evolving next position of node
		public string clan  { get; private set; }
		//this is the head of a trust group iff trustHead == this
		public Node trustHead  { get; private set; }//the node whose trustGroup I belong to. Default == this
		public List<Node> trusters  { get; private set; } //back pointers from OTHER nodes trustHead pointers, all OTHER nodes whose trustHead == this
		private int pcOffset;

		public float pushedMinusPulled;
		public Dictionary<string,int> states; //Keys are independent state dimensions.

		public List<Rules.Rule> rules;

		public Node givenTarget {get; private set;}

		public Node(int id0, float x0, float y0, float radius0 ){
			id = id0;
			x = x0; y = y0; 
			setRadius(radius0);
        	oomph = 0.0f;
			dna = 0L;
			bones = new List<Bone>();
			neighbors = new List<Node>();
			trusters = new List<Node>();
			states = new Dictionary<string,int>();
			rules = new List<Rules.Rule>();
        	this.naiveState();
		}

		//does not change position, radius nor dna
		public void naiveState(){

			retakeBurden();
			clearTrust();
			clearBones();
			neighbors.Clear();
			states.Clear();
			rules.Clear();
			nx = x; //node.nx,ny the hallucinated potential evolving next position of node
			ny = y;
			clan = getRandomClan(); // not likely to be the clan of anything else in the world, nearly inevitably unique
			states = new Dictionary<string,int>();
			pcOffset = (int) Random.Range(0,100);

			//this.smarts = undefined;
			//this.intrface = undefined;
		}

		public void clearTrust(){
			trustHead = this;
			foreach (Node n in trusters) n.trustHead = n;
			trusters.Clear ();
		}

		//adds truster to the trusters of whoever this trusts...
		public void addTruster(Node truster){
			if (trustHead.trusters.Contains(truster)) return;
			truster.trustHead = trustHead;
			trustHead.trusters.Add(truster);
		}

		public void trust(Node head){
			if (trustHead == head.trustHead) return; //I already do

			//everyone who trusted me now trusts whoever head trusts
			foreach (Node n in trustHead.trusters){
				head.trustHead.addTruster(n);
			}

			trustHead.trusters.Clear();

			//and I trust whoever head trusts
			trustHead = head.trustHead;
			head.trustHead.addTruster (this);

		}

		//trusts is symmetric
		public bool trusts(Node n){
			return this.trustHead == n.trustHead;
		}

		public void breakTrust(){
			if (trustHead == this) return; //don't break trust in self. Implies that a trustHead can't leave an org
			trustHead.trusters.Remove(this);
			trustHead = this;
		}

		//set of nodes that trust each other to shift burden and share oomph and make bones.
		public List<Node> trustGroup()
		{	if (trustHead != this)return trustHead.trustGroup();

			List<Node> group = new List<Node>();
			group.Add (this);
			foreach (Node n in trusters) group.Add (n);
			return group;
		}

		public void enableMuscles(int percent){
			for (int i = 0; i<rules.Count; i++) rules[i].enableMuscles(percent);
		}

		public void setState(string key, int value){
				states[key] = value;
		}

		//does the given state exist (regardless of what its value is)?
		public bool checkState(string state){
			int val;
			//ignore value, the given state is a boolean state dimension, is either there or not
			return (states.TryGetValue(state, out val)); 		
		}

		//check to see if the state exists and has the given value
		public bool checkState(string state, int value){
			int val;
			return (states.TryGetValue(state, out val) && value == val);
		}

		//if state not present, returns int.MinValue
		public int getState(string state){
			int val;
			if (states.TryGetValue(state, out val)) return val;
			else return int.MinValue;
		}

		public string dumpStates(){
			string s = "";
			foreach (var v in states) s += v.Key +":"+ v.Value + "  ";
			return s;
		}


		public void setPcOffset( int k){ pcOffset = k; } //, random naivestate is good.

		public int pc { get{ return pcOffset + Engine.tickCounter;}}


		// units: 
		//  d, unit of distance in world coordinate system (think meters)
		//  f, fixedFrame time interval (think seconds), the inverse of fixedFrame rate
		//  o, oomph
		//  b, burden. Think friction, so analogous to a real-world force but there is no notion of mass nor accelleration 
		//  (newton unit of force is kg*m/s^2, jule unit of energy = newton*meter = kg*m^2/s^2),
		//    so force has to be defined purely as friction. At perfect efficiency, 
		//    the unit burden b can be moved a unit distance d at a price of a unit oomph o
		//  o = d*b so units of o are like "burden meters"--moving a burden of 5 one step costs the same as moving a burden of 1 five steps.
		//  Power consumption is o per second, so o/f or o*fixedFrameRate


		public int numNodesInOrg(){ return 1 + trustHead.trusters.Count;}

		//a symmetric operation on this (as source) and target
		public void addBone(Node target){
			this.bones.Add  (new Bone(this, target));
			target.bones.Add(new Bone(target, this)); 
		}

		// A bone exists in both source and target nodes. This is done so that both of them "know" about the bone, and either can remove it.
		// When added or removed, it is added or removed on both sides.
		// Also, if for some reason there are multiple bones between source and target, this removes all of them.
		public void removeBone(Node target){
			target.removeHalfBone (this);
			this.removeHalfBone(target);
		}

		private void removeHalfBone(Node target){
			int indx;
			while ((indx = this.bones.FindIndex (lnk => lnk.target == target)) >= 0) this.bones.RemoveAt(indx); 
		}

		public void clearBones(){

			List<Node> targets = new List<Node>();
			for (int i=0; i<bones.Count; i++) targets.Add (bones[i].target);

			for (int i=0; i<targets.Count; i++) removeBone(targets[i]);
			if (bones.Count > 0) {
				bubbleServer.debugDisplay("clearBones error on node "+id);
				bones.Clear (); 
			}
		}

		public float naiveBurden() {return radius2; }

		public void setRadius(float r) { 
			if (r > minRadius) radius = r; 
			else radius = minRadius; 
			radius2 = radius*radius;
			//oomph has a max, burden has a min, both dependent on radius^2
			burden = naiveBurden();
			maxOomph = CScommon.maxOomph(radius);
			minBurden = naiveBurden()*minBurdenMultiplier;
		}

		private float naiveAvailableBurden(){ return naiveBurden() - minBurden; }

		//these functions return this to support function chaining
		public Node setClan(string c){ clan = c; return this; }
		public Node setOomph(float mph){ if (mph>=0) if (mph <= maxOomph) oomph = mph; else oomph = maxOomph; return this;}
		public Node setDna(int bitNumber, bool value) { 
			dna = setBit(dna, bitNumber, value); 
			//maxOomph = CScommon.maxOomph(radius,dna); 
			return this;}
		public bool testDna(int bitNumber) { return testBit(dna, bitNumber);}

		public bool overlaps(Node node) { return distance(node) < radius + node.radius; }

		
		public float distance2(Node target){
			return (x-target.x)*(x-target.x)+(y-target.y)*(y-target.y);
		}

		public float distance(Node target){
			return Mathf.Sqrt (distance2 (target));
		}

		public float relativeDistance(Node target){
			return distance(target)/radius;
		}

		//eventually should depend on stomach's health vitamins etc.
		public void photosynthesis() {
			oomph +=  photoYield*radius2 * (maxOomph - oomph )/maxOomph; //oomph will never quite attain maxOomph, photosyn gets more inefficient as it approaches maxOomph
		}

		
		public float supply { get {return oomph/maxOomph;} }

		
		public bool given(){ return givenTarget != null; }

		public float availableBurden() { return burden - minBurden; }

		public void giveBurden(Node target){
			if (givenTarget != target){
				retakeBurden(); 
				givenTarget = target;
				float givenBurden = naiveAvailableBurden();
				if (givenBurden > availableBurden()+0.00001){
					Debug.Log ("giveBurden loss" + givenBurden + " " + availableBurden ());
					givenBurden = availableBurden ();
				}
				target.burden += givenBurden;
				burden -= givenBurden;
			}
		}

		public void retakeBurden() { 
			if (givenTarget != null){
				float givenBurden = naiveAvailableBurden();
				if (givenBurden > givenTarget.availableBurden()){
					Debug.Log ("retakeBurden loss" + givenBurden + " " + givenTarget.availableBurden ());
					givenBurden = givenTarget.availableBurden ();
				}
				givenTarget.burden -= givenBurden;
				burden += givenBurden;
				givenTarget = null;
			}
		}

		// sharing oomph via virtual 'trust' link whose efficiency still degrades with distance,
		// to maintain locality of the material world.
		// Give from the best supplied to the worst supplied, i.e. from the least needy to the most needy
		public void shareOomph(){

			if (trustHead != this) return;//do nothing except on trustHeads
			List<Node> trustGrp = trustGroup();

			Node bestSupplied = trustGrp[0];
			Node worstSupplied = bestSupplied;
			for (int i = 1; i < trustGrp.Count; i++) {
				if (trustGrp[i].supply < worstSupplied.supply ) worstSupplied = trustGrp[i];
				if (trustGrp[i].supply > bestSupplied.supply ) bestSupplied = trustGrp[i];
			}

			float fairSupply = (worstSupplied.supply + bestSupplied.supply)/2;
			float mostWorstCanUse = worstSupplied.maxOomph - worstSupplied.oomph;
			float mostBestShouldGive = bestSupplied.oomph - fairSupply * bestSupplied.maxOomph; //will drop best's supply to fairSupply level
			float oomphToTransfer = Mathf.Min(mostWorstCanUse, mostBestShouldGive);
			bestSupplied.oomph -= oomphToTransfer;
			worstSupplied.oomph += oomphToTransfer*linkEfficiency(bestSupplied, worstSupplied); //with love. Less will be actually transfered, because of efficiency
			
		}

		public void rulesAccion(){ //rules may depend on the following order of evaluation
			for (int i=0;i<rules.Count;i++) rules[i].accion();
		}


		//don't debit oomph until after all muscles have been powered by same level of oomph.
		//Ensure that oomph demand never depasses oomph.
		public void rulesMuscleAction(){
			float totDemand = 0; 
			float fraction = 1;

			for (int i=0;i< rules.Count;i++) totDemand += rules[i].muscleActionDemand();
			if (totDemand>0 && oomph < totDemand) fraction = oomph/totDemand;
			for (int i=0;i< rules.Count;i++) rules[i].muscleAction(fraction);

			oomph -= fraction*totDemand; //pay for oomph dispensed. Cannot logically drive oomph below zero
			if (oomph < minPosValue) oomph = 0; //but numerically, si.
		}

		public void activateBones(){
			for (int i=0;i<bones.Count;i++) bones[i].boneAction();
		}


		//called after nx and ny have been fully hallucinated, and before they've been folded back into x and y
		public void doGravity(){
			float dx, dy, speed2, factor;
			dx = x - gCG.x; //distance from center of gravity
			dy = y - gCG.y;
			speed2 = (x - nx)*(x - nx) + (y - ny)*(y - ny);
			//speed is zero if node is not moving
			//Without speed unmoving nodes would slowly migrate to the center.
			//Without speed, during pushing part of cycle *both* ends would get moved inwards, towards the origin,
			//and during pulling *both* ends would get moved outwards, whereas
			//With speed, you effect the faster moving parts more, donc the pushed low-burden high-speed head gets moved inwards
			//more than the high-burden low-speed tail, and the pulled low-burden tail gets moved outwards
			//more than the high-burden low-spead head, imparting a rotating effect to the bot as a whole
			factor = gGravity*Mathf.Sqrt(speed2);
			// Note that if speed is too great, gravity can sling someone off the map. Need to have an upper limit on speed, 
			// or I can't use the gravity approach. The only current upper limit is imposed by link efficiency.
			
			//Greater effect on those far from origin
			if (pushedMinusPulled >= 0)
			{	nx -= dx*factor; ny -= dy*factor; //move towards gCG those being pushed. 
			} else
			{	nx += dx*factor; ny += dy*factor; //move away from gCG those being pulled
			}
		}

		public void updatePosition()
		{	
			doGravity ();

			//bring future into the present
			x = nx;
			y = ny;
			pushedMinusPulled = 0;
		}

		private List<int> registeredOrgNodes(){
			List<int> registeredIds = new List<int>();
			foreach (var node in trustGroup()) if (bubbleServer.registered(node.id)) registeredIds.Add(node.id);
			return registeredIds;
		}

		private void thisOrgBeats(Node loser){
			List<int> registeredWinners = this.registeredOrgNodes();
			if (registeredWinners.Count > 0){
				List<int> registeredLosers = loser.registeredOrgNodes();
				if (registeredLosers.Count > 0 ) {
					foreach (int loserId in registeredLosers) foreach (int winnerId in registeredWinners) bubbleServer.playerWinLose(winnerId, loserId);
					
					loser.randomRelocateOrganism();
				}
			}
		}

//		//presumption is that both this and winner are registerd
//		private void loserOrganism(Node winner){
//
//			List<int> registeredLoserIds = new List<int>();
//			foreach (var node in trustGroup()) if (bubbleServer.registered(node.id)) registeredLoserIds.Add(node.id);
//
//			List<int> registeredWinnerIds = new List<int>();
//			foreach (var node in winner.trustGroup ()) if (bubbleServer.registered(node.id)) registeredWinnerIds.Add(node.id);
//
//			foreach (int loserId in registeredLoserIds) foreach (int winnerId in registeredWinnerIds) bubbleServer.playerWinLose(winnerId, loserId);
//
//			randomRelocateOrganism();
//		}

		//preserves form and orientation of the organism
		private void randomRelocateOrganism(){
			Vector2 newPos = Bub.worldRadius * Random.insideUnitCircle;
			foreach (var node in trustHead.trusters) { 
				node.x += newPos.x - trustHead.x; node.y += newPos.y - trustHead.y; 
				node.nx = node.x; node.ny = node.y;
			}
			trustHead.x = newPos.x; trustHead.y = newPos.y;
			trustHead.nx = trustHead.x; trustHead.ny = trustHead.y;
		}

		private float orgOomph(){
			float sum = 0;
			foreach (var node in trustGroup ()) sum += node.oomph;
			return sum;
		}

		//reserve capacity, the amount of oomph the org could absorb in becoming completely maxd out.
		private float orgHunger(){
			float sum = 0;
			foreach (var node in trustGroup ()) sum += node.maxOomph - node.oomph;
			return sum;
		}

		public bool isEater(){return (!CScommon.testBit(dna, CScommon.vegetableBit));} 

	  	public void tryEat(Node node)
	    {   // If both eaters, the one having access to greater stomach.oomph eats lesser, must be different clans, must overlap
	        if (this.isEater())
	        {
	            if (!node.isEater() || this.oomph > node.oomph)
	            {
	                if (this.clan != node.clan && this.overlaps(node))
	                {
	                    //take all the org you can
						float thisOrgCanEat = this.orgHunger ();
						float nodeOrgCanGive = node.orgOomph ();
						float canTransfer = Mathf.Min(thisOrgCanEat, nodeOrgCanGive);
						
//						Debug.Log ("eat "+thisOrgCanEat+" "+nodeOrgCanGive+" " + canTransfer);
//						Debug.Log (this.trustGroup ().Count+":"+node.trustGroup().Count);
//						Debug.Log ("this(0) " + this.trustGroup()[0].maxOomph + "-" + this.trustGroup()[0].oomph);
//						Debug.Log ("that(0) " + node.trustGroup()[0].maxOomph + "-" + node.trustGroup()[0].oomph);

						if (canTransfer > 0){ //guard against zeroDivide by zero thisOrgCanEat or nodeOrgCanGive
							foreach (var orgN in this.trustGroup()) orgN.oomph += canTransfer*(orgN.maxOomph-orgN.oomph)/thisOrgCanEat;
							foreach (var orgN in node.trustGroup()) { 
								orgN.oomph -= canTransfer*(orgN.oomph/nodeOrgCanGive);
								if (orgN.oomph < minPosValue) orgN.oomph = 0;
							}
						}
						//could take all their burden too

						thisOrgBeats(node);
					
						/*
						switch (this.eatPolicy)
	                    {
	                        case "chomp":
	                            //Most primitive policy: If they are a stomach, take all their oomph (which is all the whole bot's oomph) and 
	                            //kill the whole bot.
	                            this.stomach.oomph += node.oomph + (node.burden - node.MinBurden); //take all their oomph and burden. burden to oomph conversion
	                            node.burden = node.minBurden;
	                            node.oomph = 0;
	                            node.deleteOrphanedLinksR();//will reset to nativeState, i.e. change clan, and clobber node.smarts.
	                            break;
	                        case "suck":    //take all its oomph, but leave it to grow again
	                        default:
	                            this.oomph += node.oomph;
	                            node.oomph = 0;
	                    } //other cases: "keep" would addBotToBotR. Do I assume there is a tractor present? No, but must either use it or destroy
	                      //it first, before adding BotToBotR. Sets clan, and removes all smarts. 
	                      // Install other smarts or consume all burden down to minBurden,
	                      //else will be more than energy min to drag them along (you always must pay at least that much).
						*/
					}
	            }
	        }
	    }
	    //Other kill policies could be envisioned, by request to my smarts function, so that intelligence can perform various kinds of eats:
	    //eating of things other than the stomach (removing all links in and out of the eaten links AND 
	    //removing any references (like tail1)in any smarts fcn (the former bot may have had several nodes with smarts fcns, not just stomach))
	    //consider implementing node.backLinks[] an array of nodes that have links to node, maintaining that in deleteLink, etc.
	    //In particular: if a different bot targets you (attaches a tractor link to you) do you become aware of that? Does knowing involve
	    //knowing which node is at the other end of that link? Opens the possibility of code to edit, vandalize, that bot. Of course
	    //the neighbors list already does that.

		//These are heuristic, we'll see how effective they are...
		public Node mostTastyNeighbor(){
	
			float bestOomph = 0f;
			Node them = null, bestThem = null;

			for (int i=0;i<neighbors.Count;i++)
			{	them = neighbors[i];
				if (them.clan != clan)
				{	if (them.isEater() && them.oomph*linkEfficiency(them,this) > this.oomph*linkEfficiency(this,them)) return null; //oops, there's somebody dangerous out there
					float this2theirs = them.oomph*linkEfficiency(this,them);
					if (this2theirs > bestOomph) {bestOomph = this2theirs; bestThem = them;}
				}
			}

			//if (bestOomph < maxOomph/1000.0) return null; //they're not worth it to me //NO, you want these to steer you back to the crowd
			return bestThem;
		}

		public Node mostDangerousNeighbor(){
			
			float bestOomph = 0f;
			Node them = null, bestThem = null;
			
			for (int i=0;i<neighbors.Count;i++)
			{	them = neighbors[i];
				if (them.clan != clan) //assume trustgroup is a subset of clan
				{	if (them.isEater() && them.oomph*linkEfficiency(them,this) > this.oomph*linkEfficiency(this,them)){ //heuristic for which of us would win a struggle
						float mine2Them = this.oomph*linkEfficiency(them,this); //heuristic for how much I'm worth to them
						if (mine2Them > bestOomph) {bestOomph = mine2Them; bestThem = them;}
					}
				}
			}

			if (bestThem == null) return null;
			if (bestOomph < bestThem.maxOomph/1000.0) return null; // I figure I'm not worth it to them
			return bestThem;
		}
		
		// The effect of muscles as friction motors is rationalized in
		// https://docs.google.com/document/d/14xpTCuiDns5AyiM-Od-4ZTVE5LU4yoOv3AW4sYux_LU/edit?usp=sharing
		// activatemuscles is called every frame tick.
		// burden is analogous to friction between the node and the world coordinate "surface".
		// A link between two nodes having large burdens (large coefficients of friction) moves them more slowly 
		// than the equivalent link between two small burdens. 
		// Dragging burden costs energy, so displacing two 'heavy' objects is going to take more energy than displacing two 'light' objects.

	}
}

