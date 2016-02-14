//copyright 2016 John Fairfield

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bubbles{
	public class Node {

		//physics fundamental tuning constants
		public static readonly float minBurdenMultiplier = 0.1f; //governs how more efficient use of shiftBurden is, than solo movement. If 1, inchworms more comperable to solos.

		// photoyield should be slow enough that inchworms that don't eat should be slow. 
		// That makes solos that don't eat minBurdenMuliplier/2 as fast, since they can't shiftBurden, and there's not two photosynthesizers
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
			Debug.Assert (!float.IsNaN (gCG.x) && !float.IsNaN (gCG.y),"resetGravity bad gCG");
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
		public float minBurden { get; private set; }
		public float maxOomph { get; private set; }

		public long dna  { get; private set; }
		public float oomph;
		public float burden;

		public List<Bone> bones;
		public List<Rules.Rule> rules;

		public Dictionary<string,int> states;
		public Org org;

		private int pcOffset;

		public float pushedMinusPulled;

		public Delaunay.Site site; //assigned during voronoi calc to return voronoi neighbors via site.neighbors(i), site.neighborsCount()

		public Node givenTarget {get; private set;}

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

		//Restores burden. Does not change position, radius, oomph, dna, pcOfset. Makes the node have individual org of team 0 and independent clan
		public void isolate(){
			retakeBurden();
			clearBones();
			rules.Clear();
			states.Clear();
			org = new Org (this);
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
		//  b, burden. Think friction, so analogous to a real-world force but there is no notion of mass nor accelleration 
		//  (newton unit of force is kg*m/s^2, jule unit of energy = newton*meter = kg*m^2/s^2),
		//    so force has to be defined purely as friction. At perfect efficiency, 
		//    the unit burden b can be moved a unit distance d at a price of a unit oomph o
		//  o = d*b so units of o are like "burden meters"--moving a burden of 5 one step costs the same as moving a burden of 1 five steps.
		//  Power consumption is o per second, so o/f or o*fixedFrameRate



		//a symmetric operation on this (as source) and target
		public void addBone(Node target){

			this.bones.Add (new Bone (this, target));
			target.bones.Add (new Bone (target, this)); 
			
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

		//IF there is a simple bone chain of nodes, and this is an interior (non extremal) element of chain, and priorN is a neighbor of this in the chain,
		//THEN otherNeighbor returns the neighbor on the other side of priorN, or null if none exists. Can be used to traverse chain in either direction.
		public Node otherBoneChainNeighbor(Node priorN){
			foreach (var bone in bones)
				if (bone.target != priorN) return bone.target;
			return null;
		}

		// concerning rules, which have muscles sourced in this node

		public void enableInternalMuscles(int percent){
			for (int i = 0; i<rules.Count; i++) rules[i].enableInternalMuscles(percent);
		}

		public int cutExternalMuscles(){
			int sum = 0;
			for (int i=0; i<rules.Count; i++) sum += rules[i].cutExternalMuscles();
			return sum;
		}

		//	public void cutMusclesTargetingOrg(Node orgMember){
		//		for (int i=0; i<rules.Count; i++) rules[i].cutMusclesTargetingOrg(orgMember);
		//	}



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
		
		public Node setTeam(int value){
			setDna (CScommon.leftTeamBit, CScommon.rightTeamBit, value);
			return this;
		}

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
				foreach (var node in this.org.members) { 
					node.oomph -= thisSends*(node.oomph/thisOrgCanGive);
					if (node.oomph < Node.minPosValue) node.oomph = 0; //mostly to make sure it never goes a smidgeon negative
				}
			}

			if (targetReceives>0) {
				foreach (var node in target.org.members) node.oomph += targetReceives*(node.maxOomph-node.oomph)/targetOrgCanEat;
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
			
		public void shareOomph() {
			if (this == org.head)
				org.shareOomph();
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
			if (oomph < Node.minPosValue) oomph = 0; //but numerically, si.
		}

		public void activateBones(){
			for (int i=0;i<bones.Count;i++) bones[i].action();
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
			//With speed, you effect the faster moving parts more, donc the pushed low-burden high-speed head gets moved inwards
			//more than the high-burden low-speed tail, and the pulled low-burden tail gets moved outwards
			//more than the high-burden low-spead head, imparting a rotating effect to the bot as a whole
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
			foreach (var node in org.members) if (bubbleServer.registered(node.id)) registeredIds.Add(node.id);
			return registeredIds;
		}

		private void thisOrgBeats(Node loser){
			if (bubbleServer.registered(org.head.id) && bubbleServer.registered(loser.org.head.id)){
				Engine.scheduledOrgRelocations.Add(loser.org.head);
				bubbleServer.scoreWinner(org.head.id);
				bubbleServer.scoreLoser(loser.org.head.id);
			}
		}

		//cuts links from this organism to loser organism
		//	private void cutOrgsMusclesToOrg(Org org){
		//		foreach (var memb in org.members) memb.cutMusclesTargetingOrg(org);
		//	}

		//cuts all external links to this organism, and all external links from this organism
		private void cutOutOrganism(){
			
			//take out all muscles attacking me
			List<Muscle> attackers = new List<Muscle>(org.enemyMuscles);
			//Debug.Log (attackers.Count + " attackers cut.");
			foreach (var muscl in attackers) muscl.cut ();

			//take out all of my attack muscles
			int sum = 0;
			foreach (var memb in org.members) sum += memb.cutExternalMuscles();
			//Debug.Log ("org " + org.members.Count + " externals cut:" + sum);
		}

		//preserves form and orientation of the organism. Called when x==nx, y==ny, and preserves that.
		public void randomRelocateOrganism(){
			Vector2 here = new Vector2(org.head.x, org.head.y);
			Vector2 delta = (Bots.worldRadius * Random.insideUnitCircle)-here;

			foreach (var memb in org.members) { 
				memb.nx = memb.x += delta.x; 
				memb.ny = memb.y += delta.y;
			}
		}

		private float orgOomph(){
			float sum = 0;
			foreach (var memb in org.members) sum += memb.oomph;
			return sum;
		}

		//reserve capacity, the amount of oomph the org could absorb in becoming completely maxd out.
		private float orgHunger(){
			float sum = 0;
			foreach (var memb in org.members) sum += memb.maxOomph - memb.oomph;
			return sum;
		}

		public bool isEater(){return (CScommon.testBit(dna, CScommon.eaterBit));} 

		private void orgEatOrg(Node eaten){

			//take all the oomph you can
			float thisOrgCanEat = this.orgHunger ();
			float eatenOrgCanGive = eaten.orgOomph ();
			float canTransfer = Mathf.Min(thisOrgCanEat, eatenOrgCanGive);

			//			if (UnityEngine.Debug.isDebugBuild && eaten.id == 0){
			//				bubbleServer.debugDisplay ("eat "+thisOrgCanEat+" "+eatenOrgCanGive+" " + canTransfer);
			//				bubbleServer.debugDisplay (this.trustGroup ().Count+":"+eaten.trustGroup().Count);
			//				bubbleServer.debugDisplay ("this(0) " + this.trustGroup()[0].maxOomph + "-" + this.trustGroup()[0].oomph);
			//				bubbleServer.debugDisplay ("that(0) " + eaten.trustGroup()[0].maxOomph + "-" + eaten.trustGroup()[0].oomph);
			//			}

			if (canTransfer > 0){ //guard against zeroDivide by zero thisOrgCanEat or eatenOrgCanGive
				foreach (var memb in this.org.members) memb.oomph += canTransfer*(memb.maxOomph-memb.oomph)/thisOrgCanEat;
				foreach (var memb in eaten.org.members) { 
					memb.oomph -= canTransfer*(memb.oomph/eatenOrgCanGive);
					if (memb.oomph < Node.minPosValue) memb.oomph = 0; //mostly to make sure it never goes a numeric smidgeon negative
				}
			}
		}

		public void tryEat(Node node)
		{   // If both eaters, the one having access to greater stomach.oomph eats lesser, must be different clans, must overlap
			if (this.isEater())
			{
				if (!node.isEater() || this.oomph > node.oomph)
				{
					if (this.clan != node.clan && this.overlaps(node))
					{
						orgEatOrg(node); //transfer of oomph
						//could take all their burden too

						node.cutOutOrganism(); //cut all muscles, from/to anywhere to/from the loser org

						org.disassembleAndTow(node.org); //only jeeps do anything

						thisOrgBeats(node); //keep score

						/*
							could also consider destroying eaten organism (all bones and muscles),
							moving them into an inventory (to be used later to assemble stuff. Don't want to make transport so easy), 
							eating burden converting it to oomph, etc
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
				if (them.org != org) { //don't try to steer by pushing on self
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

