//Bubbles world physics engine

using UnityEngine;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;

public class Bub {
	
	public static readonly float minRadius = 0.0000001f;  //bottom of fractal possibilities, don't want to push float representation
	public static readonly float minPosValue = 0.0000001f;
	//physics fundamental tuning constants
	public static readonly float minBurdenMultiplier = 0.1f; //governs how more efficient use of shiftBurden is, than solo movement. If 1, inchworms more comperable to solos.

	// photoyield should be slow enough that inchworms that don't eat should be slow. 
	// That makes solos that don't eat minBurdenMuliplier/2 as fast, since they can't shiftBurden, and there's not two photosynthesizers
	public static float photoYield = 0.08f;
	public static float baseMetabolicRate = 0.0035f;
	public static float worldRadius = 400f;

	private static float gGravity;//see resetGravity. OccaSsional large perterbations in gravity can have a pleasing and disruptive effect on circling
	private static Vector2 gCG = new Vector2(0.0f,0.0f); //Center of world is 0,0, but CG, Center of Gravity, can be moved about a bit to give a nice effect
	private static float maxRelSpeed = CScommon.inefficientLink/2; // maximum relative speed (movement in one fixedframe /radius).

	public struct SteeringStruct{
		public Node target;
		public float sideEffect; //positive if pull will pull you to the left, negative if pull will pull you to the right, and proportional to the steering effect
	}


	public static bool checkVals(float x, float y, string msg){
		bool b = false;
		if (float.IsNaN(x) || float.IsNaN (y)) { if (UnityEngine.Debug.isDebugBuild) bubbleServer.debugDisplay ("XXX Nan: "+msg); b = true;}
		if (float.IsInfinity(x) || float.IsInfinity(y)) { if (UnityEngine.Debug.isDebugBuild) bubbleServer.debugDisplay ("XXX Inf: "+msg); b = true;}
		return b;
	}

	public static void resetGravity(){
		float norm = -1.7f*Mathf.Log(worldRadius); //larger neg value makes gravity weaker
		gGravity = Mathf.Exp(Random.Range(1.25f*norm, 0.75f*norm)); //assumes norm is negative.
		gCG.x = 0.2f*worldRadius*(Random.value - 0.5f);
		gCG.y = 0.2f*worldRadius*(Random.value - 0.5f);
		Debug.Assert (!float.IsNaN (gCG.x) && !float.IsNaN (gCG.y),"resetGravity bad gCG");
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



	//bitNumber 0 is least significant bit. MUST BE IDENTICAL TO CScommon.testBit
	public static bool testBit(long dna, int bit)
	{ return ((dna & (1L << bit)) != 0L);}

	public static long setBit(long dna, int bit, bool value)
	{	if (testBit(dna, bit) != value) dna = dna ^ (1L<<bit);
		return dna;
	}

	//leftBit >= rightBit specifies a field of bits within dna. SetBit sets that field of the dna to the given value.
	//Any bits of value above the field size will be ignored.
	//Bits can be recovered using CScommon.dnaNumber(dna,leftBit,rightBit)
	public static long setBit(long dna, int leftBit, int rightBit, int value)
	{	long mask = (1<<(1+leftBit-rightBit))-1; // difference of 0 becomes 1b, 1 becomes 11b, 2 becomes 111b etc
		long lvalue = ((long)value & mask) << rightBit; // kill off all but that many bits of value, and shift left into place
		mask = ~(mask << rightBit); //shift mask into place and complement, so it is a bunch of 1's with a hole (of zeros) in it
		return (dna & mask) | lvalue; // clear DNA in the hole, and put value into the hole
	}


	/////////////////////////////////////////////////////////////////////////////////// 
	
	public class Muscle {
		
		public Node source { get; protected set; }
		public Node target { get; private set; }

		public float demand {get; private set;}
		private float pastDemand;

		public bool external {get{ return target.trustHead != source.trustHead;}} // a cut muscle is not external. Nor is it internal.

		public void reTarget(Node n) {
			if (external) target.enemyMuscles.Remove(this); 
			target = n;
			if (external) target.enemyMuscles.Add(this); 
		}

		public bool enabled {get{ return demand > 0;}} 
		public bool disabled { get {return demand == 0;}}

		// cut implies disabled, but disabled does not imply cut.
		public Muscle cut() { reTarget(source); pastDemand = demand = 0; return this;}
		public bool notCut { get {return target != source;}}

		public Muscle enable(int percent){
			if ( notCut && percent>=0 && percent <= 300 ) {
				demand = source.radius2 * baseMetabolicRate * percent;//some day * CScommon.testBit(source.dna, CScommon.strengthBit)?10:1; 
				pastDemand = demand; //so that a subsequent reEnable will do nothing.
			}
			return this;
		}

		public Muscle disable(){ if (demand > 0) pastDemand = demand; demand = 0; return this; }
		public Muscle reEnable(){ if (notCut) demand = pastDemand; return this;}

		public int enabledStep(){
			return Mathf.RoundToInt(demand/(source.radius2 * baseMetabolicRate * 100)); //should be 0, 1, 2, or 3
		}

		private bool pulling;
		public bool isPuller() {return pulling;}
		public bool isPusher() { return !pulling;}
		public Muscle makePuller() {pulling = true; return this;}
		public Muscle makePusher() {pulling = false; return this;}
		public Muscle togglePushPull() {pulling = !pulling; return this;}
		public CScommon.LinkType commonType() {return pulling?CScommon.LinkType.puller:CScommon.LinkType.pusher;}
		
		public Muscle( Bub.Node source0, Bub.Node target0) {
			source = source0; //may not be null
			target = target0; //may not be null
			//is cut if source == target
			if (notCut) { 
				enable(100); 
				if (external) target.enemyMuscles.Add(this); 
			}
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
		
//		// friction narrative: the force of a link operates independently on both ends. 
//		// A better way of putting it: each end gets half the oomph expended on it. That way, the amount one end moves
//		// is independent of how much the other end moves--it all depends on their individual burdens.
//		// The unit of oomph is a burden meter
//		// So total amount of lengthening or shortening is
//		private float oomphToDisplacement(float omp){
//			return 0.5f*omp*(1/source.burden + 1/target.burden);
//		}
//		
//		private float displacementToOomph(float disp){
//			return 2*disp/(1/source.burden + 1/target.burden);
//		}


		// must not debit oomph finally until all muscles have partaken, else later muscles would have less oomph than earlier muscles
		public float actionDemand()
		{	if (isPuller() && length() < source.ceasePullDistance()) return 0;
			return demand;
		}

		// Executes the muscle action given sources's perhaps limited ability to power the muscle
		public void action(float fraction){
			float dx, dy, deliveredOomph, displacement, effect, len = length();

			if (len==0) return;
			if (isPuller() && len <= source.ceasePullDistance()) return;
			if ( fraction*demand == 0) return;

			dx = target.x - source.x;
			dy = target.y - source.y;

			deliveredOomph = fraction*demand*efficiency();
			if (deliveredOomph <= 0) return;

			// friction narrative: the force of a link operates independently on both ends. 
			// A better way of putting it: energy of action == energy of reaction, i.e.
			// each end gets half the oomph expended on it. That way, the amount one end moves
			// is independent of how much the other end moves--it all depends on their individual burdens.
			// The unit of oomph is a burden meter

			// Effect on one end is independent of effect on the other.
			// Each experiences the same 'force', they react to it in inverse proportion to their burden.
			// oomph is displacement of a unit burden,
			// but in general both ends don't have unit burden
			// Net: gap between them will shorten (puller) by displacement, where
			displacement = 0.5f*deliveredOomph/source.burden + 0.5f*deliveredOomph/target.burden;

			// insure that puller doesn't overshoot zero.
			// Further, ensure that length stays north of an epsilon:
			// if length gets too close to zero, numerical effects can result in node superposition
			// Further, ensure that puller never pulls you closer than you need to eat, to keep nodes from being
			// so confounded that it's difficult for players to distinguish them.
			// Also, perfect superposition is hard on voronoi, and 
			// you can't start pushing on it with any notion of direction of push.

			if (isPuller()){ 
				//note that at this point we have len > ceasePullDistance()
				if ( displacement > len - source.ceasePullDistance()) {
					float x = (len-source.ceasePullDistance())/displacement;
					deliveredOomph *= x;
					displacement *= x;
				}
				// give a sign to oomph-- pos if pushing (trying to increase length), neg if pulling
				deliveredOomph = -deliveredOomph; 
				displacement = -displacement;
			}

			source.pushedMinusPulled += displacement; // accumulate positive if pushed, negative if pulled
			target.pushedMinusPulled += displacement; // gravity question: should this be weighted (as below) by inverse burden?

			//change units from oomph = burden*meter to burden*meter/meter, i.e. burden * fraction of length, and cut it in half to equally apply it to both ends
			effect = deliveredOomph/(2*len); //so effect/burden is dimensionless
		
			// has full effect on unit burden
			//A smaller burden moves more than a bigger burden.
			source.nx -= dx*effect/source.burden;
			source.ny -= dy*effect/source.burden;

			target.nx += dx*effect/target.burden;
			target.ny += dy*effect/target.burden;


		}
	}


	//////////////////////////////////////////////////////////////// bones

	public static float boneStiffness = 0.35f;

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
		{	float dx = target.x - source.x;
			float dy = target.y - source.y;
			float dislocation, effect, dist;

			if (dx == 0 && dy == 0) {//bone has no notion of what direction to push
				float v = Random.Range (-Mathf.PI, Mathf.PI);
				dx = Mathf.Cos (v)*minPosValue; dy = Mathf.Sin(v)*minPosValue; dist = minPosValue;
			}
			else dist = source.distance(target);

			dislocation = boneLength - dist;
//			if (boneLength > 20){
//				if (UnityEngine.Debug.isDebugBuild) bubbleServer.debugDisplay ("BONEACTION "+boneLength+","+source.distance (target));
//				if (UnityEngine.Debug.isDebugBuild) bubbleServer.debugDisplay ("("+source.x + ","+source.y+") ("+target.x+","+target.y+")");
//				if (UnityEngine.Debug.isDebugBuild) bubbleServer.debugDisplay ("dx,dy "+dx+","+dy);
//			}

			effect = boneStiffness*dislocation;// - if too long, + if too short.

			// Effect on one end is independent of effect on the other.
			// Each experiences the same 'force', they react to it in inverse proportion to their burden.
			//A smaller burden moves more than a bigger burden. Bone force is not per unit of burden,
			//it is structural, i.e. bones between large masses are, in muscle terms, much stronger than
			//bones between small burdens.

			effect *= target.burden /(source.burden + target.burden);

			//if (source.burden + target.burden ==0) if (UnityEngine.Debug.isDebugBuild) bubbleServer.debugDisplay ("boneAction burdens zero!");
			//checkVals(effect,effect,"bonaction bone effect "+source.id+":"+target.id);

			source.nx -= effect*dx/dist;
			source.ny -= effect*dy/dist;

//			if (boneLength > 20) if (UnityEngine.Debug.isDebugBuild) bubbleServer.debugDisplay("  effect "+effect+": ("+(-dx*effect)+","+(-dy*effect)+")");
//
//			checkVals(source.nx, source.ny, "bonaction nxny "+source.id+":"+target.id);

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

		public List<Muscle> enemyMuscles;

		public float nx, ny; //hallucinated evolving next position of node
		public string clan  { get; private set; }
		//this is the head of a trust group iff trustHead == this
		public Node trustHead  { get; private set; }//the node whose trustGroup I belong to. Default == this
		public List<Node> trusters  { get; private set; } //back pointers from OTHER nodes trustHead pointers, all OTHER nodes whose trustHead == this
		private int pcOffset;

		public float pushedMinusPulled;
		public Dictionary<string,int> states; //Keys are independent state dimensions.

		public List<Rules.Rule> rules;

		public Delaunay.Site site; //assigned during voronoi calc to return voronoi neighbors via site.neighbors(i), site.neighborsCount()

		public Node givenTarget {get; private set;}

		public Node(int id0, float x0, float y0, float radius0 ){
			id = id0;
			x = x0; y = y0; 
			setRadius(radius0);
        	oomph = 0.0f;
			dna = 0L;
			bones = new List<Bone>();
			trusters = new List<Node>();
			states = new Dictionary<string,int>();
			rules = new List<Rules.Rule>();
			enemyMuscles = new List<Muscle>();
        	this.naiveState();
		}

		//does not change position, radius nor dna
		public void naiveState(){

			retakeBurden();
			clearTrust();
			clearBones();
			states.Clear();
			rules.Clear();
			enemyMuscles.Clear();
			nx = x; //node.nx,ny the hallucinated potential evolving next position of node
			ny = y;
			clan = getRandomClan(); // not likely to be the clan of anything else in the world, nearly inevitably unique
			states = new Dictionary<string,int>();
			pcOffset = (int) Random.Range(0,100);

			//this.smarts = undefined;
			//this.intrface = undefined;
		}

		public Vector2 vector2(){
			return new Vector2(x,y);
		}

		public void clearTrust(){
			trustHead = this;
			foreach (Node n in trusters) n.trustHead = n;
			trusters.Clear ();
		}

		//adds truster to the trusters of whoever this trusts...
		public void addTruster(Node truster){
			if (truster == trustHead) return; //never add a node to its own trusters list
			if (trustHead.trusters.Contains(truster)) return; //nor twice
			truster.trustHead = trustHead;
			trustHead.trusters.Add(truster);
		}

		public void trust(Node head){
			if (trustHead == head.trustHead) return; //I already do

			if (trustHead == this){
				//everyone who trusted me now trusts whomever head trusts
				foreach (Node n in trusters){
					head.trustHead.addTruster(n);
				}

				trusters.Clear();
			}

			//and I trust whomever head trusts
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
		{	if (trustHead != this) return trustHead.trustGroup();

			List<Node> group = new List<Node>(trusters);
			group.Add (this);
			return group;
		}

		//center of balance
		public Vector2 orgCOB(){
			Vector2 response;
			response.x = trustHead.x;
			response.y = trustHead.y;
			for (int i=0; i<trusters.Count; i++) {response.x += trusters[i].x; response.y += trusters[i].y;}
			response /= trusters.Count + 1;
			return response;
		}

		//direction from COB to trustHead
		public float orgOrientation(){
			if (trusters.Count == 0) return 0f;
			Vector2 cb = orgCOB();
			return  Mathf.Atan2(trustHead.y-cb.y, trustHead.x-cb.x);
		}

		public int getTeam(){
			return (int) CScommon.dnaNumber (dna, CScommon.leftTeamBit, CScommon.rightTeamBit);
		}

		public void setTeam(int teamNumber){
			setDna(CScommon.leftTeamBit, CScommon.rightTeamBit, teamNumber);
		}

		//unused
//		public Rules.Rule removeAI(){
//			Rules.Rule ai;
//			for (int i=0; i<rules.Count; i++) if (rules[i].amAI){
//					ai = rules[i];
//					rules.RemoveAt(i);
//					return ai;
//				}
//			return null;
//		}
//
//		public List<Rules.Rule> removeAIs(){
//			List<Rules.Rule> ais = new List<Rules.Rule>();
//			for (Rules.Rule ai = removeAI(); ai != null; ai = removeAI()){ ais.Add(ai); }
//			return ais;
//		}

		public void enableInternalMuscles(int percent){
			for (int i = 0; i<rules.Count; i++) rules[i].enableInternalMuscles(percent);
		}

		public void cutExternalMuscles(){
			for (int i=0; i<rules.Count; i++) rules[i].cutExternalMuscles();
		}

		public void cutMusclesTargetingOrg(Node orgMember){
			for (int i=0; i<rules.Count; i++) rules[i].cutMusclesTargetingOrg(orgMember);
		}

		//don't want to waste energy on pulling when you've already pulled enough
		//don't want to pull until nodes have identical x and y
		public float ceasePullDistance() {return 0.05f*radius;}

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
				if (UnityEngine.Debug.isDebugBuild) bubbleServer.debugDisplay("clearBones error on node "+id);
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
		public Node setDna(int leftBit, int rightBit, int value) { 
			dna = setBit(dna, leftBit, rightBit, value); 
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
			if (testDna(CScommon.noPhotoBit)) return;
			oomph +=  photoYield*radius2 * (maxOomph - oomph )/maxOomph; //oomph will never quite attain maxOomph, photosyn gets more inefficient as it approaches maxOomph
		}

		public void bless(Node target){

			float targetOrgCanEat = target.orgHunger ();
			float thisOrgCanGive = this.orgOomph()/2;
			float thisSends = Mathf.Min(targetOrgCanEat, thisOrgCanGive);
			float targetReceives = thisSends * linkEfficiency(this,target);

			if (thisSends > 0) {//guard against zeroDivide
				foreach (var orgN in this.trustGroup()) { 
					orgN.oomph -= thisSends*(orgN.oomph/thisOrgCanGive);
					if (orgN.oomph < minPosValue) orgN.oomph = 0; //mostly to make sure it never goes a smidgeon negative
				}
			}

			if (targetReceives>0) {
				foreach (var orgN in target.trustGroup()) orgN.oomph += targetReceives*(orgN.maxOomph-orgN.oomph)/targetOrgCanEat;
			}
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
					if (UnityEngine.Debug.isDebugBuild) bubbleServer.debugDisplay ("giveBurden loss" + givenBurden + " " + availableBurden ());
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
					if (UnityEngine.Debug.isDebugBuild) bubbleServer.debugDisplay ("retakeBurden loss" + givenBurden + " " + givenTarget.availableBurden ());
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
			if (trusters.Count == 0) return; //nothing to share
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
			if (totDemand <= 0) return; //all muscles disabled or are pullers within ceasePullDistance
			if (oomph < totDemand) fraction = oomph/totDemand;
			for (int i=0;i< rules.Count;i++) rules[i].muscleAction(fraction);

			oomph -= fraction*totDemand; //pay for oomph dispensed. Cannot logically drive oomph below zero
			if (oomph < minPosValue) oomph = 0; //but numerically, si.
		}

		public void activateBones(){
			for (int i=0;i<bones.Count;i++) bones[i].boneAction();
		}

		//called after nx and ny have been fully hallucinated, and before they've been folded back into x and y
		public void doGravity(){
			float dx, dy, speed2, speed, factor;
			dx = x - gCG.x; //distance from center of gravity
			dy = y - gCG.y;

			checkVals(gCG.x,gCG.y, "gCG"); checkVals(x,y, "x||y");
			checkVals(dx,dy,"dx||dy");
			checkVals(nx,ny,"nx||ny before speed2 calc");

			speed2 = (x - nx)*(x - nx) + (y - ny)*(y - ny);
			//speed is zero if node is not moving
			//Without speed unmoving nodes would slowly migrate to the center.
			//Without speed, during pushing part of cycle *both* ends would get moved inwards, towards the origin,
			//and during pulling *both* ends would get moved outwards, whereas
			//With speed, you effect the faster moving parts more, donc the pushed low-burden high-speed head gets moved inwards
			//more than the high-burden low-speed tail, and the pulled low-burden tail gets moved outwards
			//more than the high-burden low-spead head, imparting a rotating effect to the bot as a whole
			// Note that if speed is too great, gravity can sling someone off the map. Need to have an upper limit on speed, 
			// or I can't use the gravity approach. The only current upper limit is imposed by link efficiency.
			speed = Mathf.Sqrt(speed2);
			factor = gGravity*speed; 

			if ( !(factor<=0.999f)) factor = 0.999f;//Use negation so that if factor is NaN will fix. Use 0.999f so several points aren't all imposed directly on gCG. 

			//Greater effect on those far from origin
			if (pushedMinusPulled >= 0)
			{	nx -= dx*factor; ny -= dy*factor; //move towards gCG those being pushed. 
			} else
			{	nx += dx*factor; ny += dy*factor; //move away from gCG those being pulled
			}
			checkVals(nx,ny,"nx||ny");
		}

		private void imposeMaxRelativeSpeed(){
			float dx, dy, speed;
			float relSpeed, newRelSpeed, ratio;

			dx = nx-x; dy = ny-y;
			speed = Mathf.Sqrt((nx*nx)+(ny*ny));
			
			relSpeed = speed/radius;
			if (relSpeed < minPosValue) return; //avoid numerical problems at and close to zero

			newRelSpeed = (maxRelSpeed * relSpeed)/(maxRelSpeed + relSpeed);
			// newRelSpeed <= relSpeed (and is only equal when both zero)
			// as relSpeed approaches 0, newRelSpeed approaches relSpeed
			// as relSpeed approaches maxRelSpeed, newRelSpeed approaches maxRelSpeed/2
			// as relSpeed approaches infinity, newRelSpeed approaches maxRelSpeed
			ratio = newRelSpeed/relSpeed; // ratio between 0 and 1, at slow relSpeeds nearly 1
			// diminish relSpeed to newRelSpeed
			dx *= ratio;
			dy *= ratio;

			nx = x+dx;
			ny = y+dy;

		}

		public void updatePosition()
		{	
			doGravity ();

			imposeMaxRelativeSpeed();

			//bring future into the present, injecting a small amount of chaos to prevent convergent forces on nx,ny
			//(like a surrounding triad of pushers pushing several nodes to their midpoint) from achieving perfect overlap
			// (which is hard on voronoi calculations)
			Vector2 chaos = 0.000023f*Random.insideUnitCircle;
			x = nx = nx + chaos.x;
			y = ny = ny + chaos.y;
			pushedMinusPulled = 0;
		}

		private List<int> registeredOrgNodes(){
			List<int> registeredIds = new List<int>();
			foreach (var node in trustGroup()) if (bubbleServer.registered(node.id)) registeredIds.Add(node.id);
			return registeredIds;
		}

//		private void thisOrgBeats(Node loser){
//			List<int> registeredWinners = this.registeredOrgNodes();
//			if (registeredWinners.Count > 0){
//				List<int> registeredLosers = loser.registeredOrgNodes();
//				if (registeredLosers.Count > 0 ) {
//
//					Engine.scheduledOrgRelocations.Add(loser.trustHead);
//					foreach (int winnerId in registeredWinners) bubbleServer.scoreWinner(winnerId);
//					foreach (int loserId in registeredLosers) bubbleServer.scoreLoser(loserId);
//				}
//			}
//		}

		private void thisOrgBeats(Node loser){
			if (bubbleServer.registered(trustHead.id) && bubbleServer.registered(loser.trustHead.id)){
				Engine.scheduledOrgRelocations.Add(loser.trustHead);
				bubbleServer.scoreWinner(trustHead.id);
				bubbleServer.scoreLoser(loser.trustHead.id);
			}
		}

		//cuts links from this organism to loser organism
		private void cutOrgsMusclesToOrg(Node targetOrg){
			List<Node> org = trustGroup();
			foreach (var node in org) node.cutMusclesTargetingOrg(targetOrg);
		}

		//cuts all external links to this organism, and all external links from this organism
		private void cutOutOrganism(){
			//take out all muscles attacking me
			List<Muscle> attackers = new List<Muscle>(trustHead.enemyMuscles);
			foreach (var muscl in attackers) muscl.cut ();
			//take out all of my attack muscles
			List<Node> org = trustGroup();
			foreach (var node in org) node.cutExternalMuscles();
		}

		//preserves form and orientation of the organism. Called when x==nx, y==ny, and preserves that.
		public void randomRelocateOrganism(){
			Vector2 here = new Vector2(trustHead.x, trustHead.y);
			Vector2 delta = (Bub.worldRadius * Random.insideUnitCircle)-here;

			foreach (var node in trustHead.trusters) { 
				node.x  += delta.x; node.y += delta.y;
				node.nx  = node.x;  node.ny = node.y;
			}
			trustHead.x += delta.x;     trustHead.y += delta.y;
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
						
//						if (UnityEngine.Debug.isDebugBuild) bubbleServer.debugDisplay ("eat "+thisOrgCanEat+" "+nodeOrgCanGive+" " + canTransfer);
//						if (UnityEngine.Debug.isDebugBuild) bubbleServer.debugDisplay (this.trustGroup ().Count+":"+node.trustGroup().Count);
//						if (UnityEngine.Debug.isDebugBuild) bubbleServer.debugDisplay ("this(0) " + this.trustGroup()[0].maxOomph + "-" + this.trustGroup()[0].oomph);
//						if (UnityEngine.Debug.isDebugBuild) bubbleServer.debugDisplay ("that(0) " + node.trustGroup()[0].maxOomph + "-" + node.trustGroup()[0].oomph);

						if (canTransfer > 0){ //guard against zeroDivide by zero thisOrgCanEat or nodeOrgCanGive
							foreach (var orgN in this.trustGroup()) orgN.oomph += canTransfer*(orgN.maxOomph-orgN.oomph)/thisOrgCanEat;
							foreach (var orgN in node.trustGroup()) { 
								orgN.oomph -= canTransfer*(orgN.oomph/nodeOrgCanGive);
								if (orgN.oomph < minPosValue) orgN.oomph = 0; //mostly to make sure it never goes a smidgeon negative
							}
						}

						//cut all of winner orgs muscles to loser node's org. For both their sakes.
						cutOrgsMusclesToOrg(node); 

						//could take all their burden too

						thisOrgBeats(node); //keep score
					
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

		public Node closestStranger(){
			Node closest = null; Node nbor;
			float closestDistance = float.MaxValue;

			for (int i=0;i<site.neighborsCount();i++){
				nbor = site.neighbors(i);
				if (nbor.clan != clan && distance(nbor) < closestDistance){
					closest = nbor;
					closestDistance = distance(nbor);
				}
			}
			return closest;
		}
			
		//returns the neighbor best suited to steer the organism, in a SteeringStruct that also shows what the expected effect will be
		public SteeringStruct bestSteeringNeighbor(){

			if (trustHead != this) return trustHead.bestSteeringNeighbor();
			//this is a trustHead

			SteeringStruct best = new SteeringStruct();

			best.target = null;
			if (trusters.Count == 0) return best; //can't determine an orientation of this organism
			
			Node them = null; 
			float sideEffect;
			float suitability, bestSuitability=-1;

			Vector2 cob = orgCOB();

			for (int i=0;i<site.neighborsCount();i++)
			{	them = site.neighbors(i);
				if (them.trustHead != this) { //don't try to steer by pushing on self
					float angl = Rules.signedAngle(this,cob,them); //positive to the left, negative to the right
					sideEffect = Mathf.Sin(angl) * (them.burden/(them.burden+this.burden)) * linkEfficiency(this,them); // think pull or push orthogonal to the org orientation
					suitability = Mathf.Abs(sideEffect); //min value is 0
					if (suitability > bestSuitability) {
						best.target = them;
						best.sideEffect = sideEffect;
						bestSuitability = suitability;
					}
				}
			}
			return best; //best.target may still be null
		}

		//These are heuristic, we'll see how effective they are...
		public Node mostTastyNeighbor(){
	
			float bestOomph = 0f;
			Node them = null, bestThem = null;

			for (int i=0;i<site.neighborsCount();i++)
			{	them = site.neighbors(i);
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
			
			for (int i=0;i<site.neighborsCount();i++)
			{	them = site.neighbors(i);
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

