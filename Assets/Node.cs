//copyright 2016 John Fairfield

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bubbles{
	public class Node {

		//physics fundamental tuning constants
		public static readonly float minGripMultiplier = 0.0625f; //governs how more efficient dragging while at minGrip than at naiveGrip.

		// photoyield should be slow enough that inchworms and bugs that don't eat should be slow. 
		public static float photoYield = 0.08f;
		public static readonly float minRadius = 0.0000001f;  //bottom of fractal possibilities, don't want to push float representation
		public static float maxRelSpeed = CScommon.inefficientLink/2; // maximum relative speed (movement in one fixedframe /radius). Strong medicine, setting it much lower really slows everything down

		public static readonly float minPosValue = 0.0000001f;
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
			
		// the efficiency of a link between the two nodes.
		// public, and not a link method, so that bots can determine which potential muscles would be most efficient
		// This is independent of metabolicOutput--the node's ability to supply the link with oomph. 
		// It is wholly a function of link length AND the source's radius--so linkEfficiency is NOT symmetric
		public static float linkEfficiency(Node source, Node target){ 
			return CScommon.efficiency(distance2(source,target),source.radius2);
		}

		public static float distance2(Node n1, Node n2){
			return (n1.x-n2.x)*(n1.x-n2.x)+(n1.y-n2.y)*(n1.y-n2.y);
		}

		//gravity system

		private static float gGravity;//see resetGravity. OccaSsional large perterbations in gravity can have a pleasing and disruptive effect on circling
		private static Vector2 gCG = new Vector2(0.0f,0.0f); //Center of world is 0,0, but CG, Center of Gravity, can be moved about a bit to give a nice effect


		public static void resetGravity(){
			float norm = -1.7f*Mathf.Log(Bots.worldRadius); //larger neg value makes gravity weaker
			gGravity = Mathf.Exp(Random.Range(1.25f*norm, 0.75f*norm)); //assumes norm is negative.
				gCG.x = 0.2f*Bots.worldRadius*(Random.value - 0.5f);
				gCG.y = 0.2f*Bots.worldRadius*(Random.value - 0.5f);
			if (Debug.isDebugBuild) Debug.Assert (!float.IsNaN (gCG.x) && !float.IsNaN (gCG.y),"resetGravity bad gCG");
		}

		public static void initialize(){
			resetGravity();
		}

		public int id  { get; private set; } //this nodes' index into the nodes array
		public float x, y;
		public float nx, ny; //hallucinated evolving next position of node


		//all these functions of radius, set in setRadius
		public float radius  { get; private set; }
		public float radius2 { get; private set;}
		public float minGrip { get; private set; }
		public float maxOomph { get; private set; }

		public long dna  { get; private set; }
		public float oomph;
		public float grip;

		public List<Bone> bones;
		public List<Rules.Rule> rules;

		public Dictionary<string,int> states;
		public Org org;

		private int pcOffset;

		public float pushedMinusPulled;

		public Delaunay.Site site; //assigned during voronoi calc to return voronoi neighbors via site.neighbors(i), site.neighborsCount()

		public Node(int id0, float x0, float y0, float radius0 , Org org0 = null){
			id = id0;
			nx = x = x0; 
			ny = y = y0;
			setRadius(radius0);
			oomph = 0.0f;
			dna = 0L;
			bones = new List<Bone>();
			rules = new List<Rules.Rule>();
			states = new Dictionary<string,int> ();
			pcOffset = (int) Random.Range(0,100);
			org = (org0 == null) ? (new Org (this)) : org0;
		}

		public string clan { get { return org.clan;}}

		//Restores grip. Does not change position, radius, oomph, dna, pcOfset. 
		//Makes the node have individual org of which it is the head
		public void isolate(){
			grip = naiveGrip;
			clearBones();

			//cut all enemy muscles attacking me
			List<Muscle> attackers = new List<Muscle>(org.enemyMuscles);
			foreach (var muscl in attackers) if (muscl.target == this) muscl.cut ();

			cutAllMuscles ();

			rules.Clear();
			states.Clear();

			if (this != this.org.head) { 
				org.members.Remove (this);
				org = new Org (this);
			}
		}
				
		public Vector2 vector2(){
			return new Vector2(x,y);
		}


		//don't want to waste energy on pulling when you've already pulled enough
		//don't want to pull until nodes have identical x and y
		public float ceasePullDistance() {return 0.05f*radius;}

			
		public void setPcOffset( int k){ pcOffset = k; } //, random naive state is good.

		public int pc { get{ return pcOffset + Engine.tickCounter;}}


		// units: 
		//  d, unit of distance in world coordinate system (think meters)
		//  f, fixedFrame time interval (think seconds), the inverse of fixedFrame rate
		//  o, oomph
		//  b, grip. Think friction, so analogous to a real-world force but there is no notion of mass nor accelleration 
		//  (newton unit of force is kg*m/s^2, jule unit of energy = newton*meter = kg*m^2/s^2),
		//    so force has to be defined purely as friction. At perfect efficiency, 
		//    the unit grip b can be moved a unit distance d at a price of a unit oomph o
		//  o = d*b so units of o are like "grip meters"--moving a grip of 5 one step costs the same as moving a grip of 1 five steps.
		//  Power consumption is o per second, so o/f or o*fixedFrameRate



		//a symmetric operation on this (as source) and target
		public void addBone(Node target){
			Bone b = new Bone(this, target);
			this.bones.Add(b);
			target.bones.Add(b);
		}

		// A bone exists on both source and target nodes. This is done so that both of them "know" about the bone, and either can remove it.
		// When added or removed, it is added or removed on both source and target nodes, i.e. on both ends
		public void removeBone(int boneIndex){
			bones[boneIndex].otherEnd(this).bones.Remove(bones[boneIndex]);
			bones.RemoveAt(boneIndex);
		}

		public void clearBones(){
			for (int i=bones.Count-1; i>=0; i--) removeBone(i);
		}

//		//IF there is a simple bone chain of nodes, and this is an interior (non extremal) element of chain, and priorN is a neighbor of this in the chain,
//		//THEN otherNeighbor returns the neighbor on the other side of priorN, or null if none exists. Can be used to traverse chain in either direction.
//		public Node otherBoneChainNeighbor(Node priorN){
//			foreach (var bone in bones)
//				if (bone.otherEnd(this) != priorN) return bone.otherEnd(this);
//			return null;
//		}

		// concerning rules, which have muscles sourced in this node

		public Node enableInternalMuscles(int percent){
			for (int i = 0; i<rules.Count; i++) rules[i].enableInternalMuscles(percent);
			return this;
		}

		public int cutExternalMuscles(){
			int sum = 0;
			for (int i=0; i<rules.Count; i++) sum += rules[i].cutExternalMuscles();
			return sum;
		}

		public void cutAllMuscles(){
			for (int i = 0; i < rules.Count; i++)
				rules [i].cutAllMuscles ();
		}

		public int breakExternalBones(){
			int sum = 0;

			for (int i = bones.Count-1; i>=0; i--) {
				if (bones [i].otherEnd(this).org != org) {
					removeBone (i);
					sum += 1;
				}
			}

			return sum; //number of bones broken
		}

		//	public void cutMusclesTargetingOrg(Node orgMember){
		//		for (int i=0; i<rules.Count; i++) rules[i].cutMusclesTargetingOrg(orgMember);
		//	}


		public float naiveGrip {get { return radius2; } }

		public void setRadius(float r) { 
			if (r > minRadius) radius = r; 
			else radius = minRadius; 
			radius2 = radius*radius;
			//oomph has a max, grip has a min, both dependent on radius^2
			grip = naiveGrip;
			maxOomph = CScommon.maxOomph(radius);
			minGrip = naiveGrip*minGripMultiplier;
		}


		//these functions return this to support function chaining
		public Node setOomph(float mph){
			if (mph >= 0) {
				if (mph <= maxOomph)
					oomph = mph;
				else
					oomph = maxOomph;
			}
			return this;
		}

		public Node setDna(int bitNumber, bool value) { 
			dna = setBit(dna, bitNumber, value); 
			//maxOomph = CScommon.maxOomph(radius,dna); 
			return this;
		}

		public Node setDna(int leftBit, int rightBit, int value) { 
			dna = setBit(dna, leftBit, rightBit, value); 
			//maxOomph = CScommon.maxOomph(radius,dna); 
			return this;
		}

		public int teamNumber { get { return (int) CScommon.dnaNumber (dna, CScommon.leftTeamBit, CScommon.rightTeamBit); } }

		public bool testDna(int bitNumber) { return testBit(dna, bitNumber);}


		public bool overlaps(Node node) { return distance(node) < radius + node.radius; }

		public float hunger(){ return maxOomph - oomph; }

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
		
			if (org.isServant()) {
				float canTransfer = Mathf.Min (org.master.hunger(), oomph);
				oomph -= canTransfer;
				float transfer = canTransfer*linkEfficiency(this,org.master);
				org.master.oomph += transfer;

				int donorId = org.head.getState ("donorId"); //int.MinValue if no donorId present
				if (donorId >= 0) Score.addToProductivity(donorId,transfer);//donor gets credit for donated servant's work
			}
		}

		//presumption is that this.fuelGauge > target.fuelGauge
		public float fillToFuelGauge(Node target){
			Debug.Assert (this.fuelGauge > target.fuelGauge);

			float fairfuelGauge = (target.fuelGauge + fuelGauge)/2;
			float mostTargetShouldGet = target.maxOomph*fairfuelGauge - target.oomph; //would raise his fuelGauge to fairfuelGauge level
			float mostIShouldGive = oomph - fairfuelGauge * maxOomph; //would drop my fuelGauge to fairfuelGauge level
			float oomphToTransfer = Mathf.Min(mostTargetShouldGet, mostIShouldGive);
			oomph -= oomphToTransfer;
			oomphToTransfer *= Node.linkEfficiency(this, target); //less is delivered than was sent...
			target.oomph += oomphToTransfer;
			return oomphToTransfer;
		}

		public void bless(Node target){

			if (this.org == target.org) return; //don't bless self--decreaseOomph below increases effect of decreaseHunger

			float thisSends = Mathf.Min(target.hunger(), this.oomph/2); //donate half of what you've got
			float targetReceives = thisSends * linkEfficiency (this, target);

			this.oomph -= thisSends;
			target.oomph += targetReceives;

			if (CScommon.testBit(target.dna,CScommon.goalBit) && target.teamNumber == this.teamNumber) Score.addToProductivity (this.id,targetReceives); //I only get credit for what they receive
		}

		public float fuelGauge { get {return oomph/maxOomph;} }

		public float availableGrip() { return grip - minGrip; }

			
		public void shareOomph() {
			if (this == org.head)
				org.shareOomph();
		}

		public void rulesAccion(){ //rules may depend on the following order of evaluation
			for (int i=0;i<rules.Count;i++) rules[i].accion();
		}

		public bool mounted(){
			return testDna (CScommon.playerPlayingBit);
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
			if (oomph < Node.minPosValue) oomph = 0; //but numerically, si.
		}

		public void activateBones(){
			for (int i=0;i<bones.Count;i++) if (this == bones[i].source) bones[i].action(); //only do it once, it is symmetrical
		}

		//called after nx and ny have been fully hallucinated, and before they've been folded back into x and y
		public void doGravity(){
			float dx, dy, speed2, speed, factor;
			dx = x - gCG.x; //distance from center of gravity
			dy = y - gCG.y;

			bubbleServer.checkVals(gCG.x,gCG.y, "gCG"); 
			bubbleServer.checkVals(x,y, "x||y");
			bubbleServer.checkVals(dx,dy,"dx||dy");
			bubbleServer.checkVals(nx,ny,"nx||ny before speed2 calc");

			speed2 = (x - nx)*(x - nx) + (y - ny)*(y - ny);
			//speed is zero if node is not moving
			//Without speed unmoving nodes would slowly migrate to the center.
			//Without speed, during pushing part of cycle *both* ends would get moved inwards, towards the origin,
			//and during pulling *both* ends would get moved outwards, whereas
			//With speed, you effect the faster moving parts more, donc the pushed low-grip high-speed head gets moved inwards
			//more than the high-grip low-speed tail, and the pulled low-grip tail gets moved outwards
			//more than the high-grip low-spead head, imparting a rotating effect to the bot as a whole
			// Note that if speed is too great, gravity can sling someone off the map. Need to have an upper limit on speed, 
			// or I can't use the gravity approach. Link efficiency and maxrelspeed do some of that work.
			speed = Mathf.Sqrt(speed2);
			factor = gGravity*speed; 

			if ( !(factor<=0.999f)) factor = 0.999f;//Use negation so that if factor is NaN will fix. Use 0.999f so several points aren't all imposed directly on gCG. 

			//Greater effect on those far from origin
			if (pushedMinusPulled >= 0)
			{	nx -= dx*factor; ny -= dy*factor; //move towards gCG those being pushed. 
			} else
			{	nx += dx*factor; ny += dy*factor; //move away from gCG those being pulled
			}
			bubbleServer.checkVals(nx,ny,"nx||ny");
		}

		private void imposeMaxRelativeSpeed(){
			float dx, dy, speed;
			float relSpeed, newRelSpeed, ratio;

			dx = nx-x; dy = ny-y;
			speed = Mathf.Sqrt((nx*nx)+(ny*ny));

			relSpeed = speed/radius;
			if (relSpeed < Node.minPosValue) return; //avoid numerical problems at and close to zero

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
			foreach (var node in org.members) if (Score.registered(node.id)) registeredIds.Add(node.id);
			return registeredIds;
		}



		//cuts links from this organism to loser organism
		//	private void cutOrgsMusclesToOrg(Org org){
		//		foreach (var memb in org.members) memb.cutMusclesTargetingOrg(org);
		//	}





		public bool isEater(){return (CScommon.testBit(dna, CScommon.eaterBit) && !org.isServant());} 


		public void tryEat(Node node)
		{   // If both eaters, the one having access to greater stomach.oomph eats lesser, must be different clans, must overlap
			if (this.isEater())
			{
				if (!node.isEater() || this.oomph > node.oomph)
				{
					if (this.clan != node.clan && this.overlaps(node) && !(node.org.isServant() && node.org.master.org.clan == org.clan) ) //don't eat your own servants, indeed servants of anybody in your clan
					{
						node.org.cutOut(); //liberate all node.orgs servants, liberate node.org breaking its shackle, cut all muscles attacking it, cut all its external muscles.
							
						org.eatOomph(node.org); //transfer of oomph from node org to this org

						//either take prisoner or relocate, but not both.
						if (org.hasHitch ()) {
							org.takePrisoner(node.org);
						} else {
							if (org.bothRegistered (node.org))
								Engine.scheduledOrgRelocations.Add (node.org);
						}

						if (org.bothRegistered(node.org)) Score.scoreCoup(this.id); 

					}
				}
			}
		}

		//nearest or farthest rock, depending on bool parameter. If surrounded by non-rocks, may return null
		public Node estRock(bool nearTfarF){
			Node est = null; float estDistance=0; 
			Node nbor;
			for (int i = 0; i < site.neighborsCount (); i++) {
				nbor = site.neighbors (i);
				if (nbor.org.members.Count == 1) { //if it's a rock
					float d = distance (nbor);
					if (est == null || (nearTfarF?d < estDistance : d > estDistance)){
						est = nbor; estDistance = d;
					}
				}
			}
			return est;
		}


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
		public Rules.SteeringStruct bestSteeringNeighbor(){

			if (org.head != this) return org.head.bestSteeringNeighbor();
			//this is an org head

			Rules.SteeringStruct best = new Rules.SteeringStruct();

			best.target = null;
			if (org.members.Count == 0) return best; //can't determine an orientation of this organism

			Node them = null; 
			float sideEffect;
			float suitability, bestSuitability=-1;

			Vector2 cob = org.COB();

			for (int i=0;i<site.neighborsCount();i++)
			{	them = site.neighbors(i);
				if (them.org != org && this != them.org.master ) { //don't try to steer by pushing on self, or on one's prisoners
					float angl = Rules.signedAngle(this,cob,them); //positive to the left, negative to the right
					sideEffect = Mathf.Sin(angl) * linkEfficiency(this,them); // think pull or push orthogonal to the org orientation //? * (them.grip/(them.grip+this.grip))
					suitability = Mathf.Abs(sideEffect); //min value is 0
					if (bubbleServer.stepping)Debug.Log( them.id+"."+sideEffect ); //gggg
					if (suitability > bestSuitability) {
						best.target = them;
						best.sideEffect = sideEffect;
						bestSuitability = suitability;
						if (bubbleServer.stepping) Debug.Log ("better");
					}
				}
			}
			if (bubbleServer.stepping) Debug.Log ("best" + (best.target == null ? " null" : (" " + best.target.id)));
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
		// grip is analogous to friction between the node and the world coordinate "surface".
		// A link between two nodes having large grips (large coefficients of friction) moves them more slowly 
		// than the equivalent link between two small grips. 
		// Dragging grip costs energy, so displacing two 'heavy' objects is going to take more energy than displacing two 'light' objects.



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

		public void setState(string key, int value){
			states[key] = value;
		}

	}
}

