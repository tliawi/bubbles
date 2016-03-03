using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bubbles{
	public class Org {

		public List<Node> members; //this has Count of at least 1, and its first member is considered the head of the org for directional purposes
		public Node head { get; private set; } //convenience for members[0]
		public List<Muscle> enemyMuscles;
		
		public string clan;
		public Dictionary<string,int> states; //Keys are independent state dimensions.

		public Node hitch { get; private set; }  //a member of this org
		public Org master; //not this org.

		public Org (Node firstMember){
			members = new List<Node>();
			members.Add (firstMember);
			head = firstMember;
			enemyMuscles = new List<Muscle>();
			states = new Dictionary<string,int> ();
			setRandomClan(); // not likely to be the clan of anything else in the world, nearly inevitably unique
		}

		public Org setRandomClan() {
			clan = Random.Range(0,int.MaxValue).ToString();
			return this;
		}

		public void makeServant( Org master0) {
			if (master == null && master0!=this) master = master0; //all this photosynthesis will go to master0.head
		}

		public void dropGrip(){
			foreach (var n in members)
				n.grip = n.minGrip;
		}

		public void raiseGrip(){
			foreach (var n in members)
				n.grip = n.naiveGrip;
		}

		public bool isStripped(){
			foreach (var n in members)
				if (n.grip > n.minGrip)
					return false;
			return true;
		}

		public void makeStrippedServant(Org master0) {
			makeServant (master0);
			if (isServant ()) dropGrip();
		}

		public void makeShackledStrippedServant(Org master0){
			if (master0.hasHitch ()) {
				makeStrippedServant (master0);
				//put a shackle, an external bone, between this.head and master0.hitch
				head.addBone(master0.hitch); //master0's servants are at the otherEnd of all external bones out of master0.hitch
			}
		}

		public bool isServant(){ return master != null; } //at a minimum, all photosynth of this org is fed to master. May also be stripped of grip, and may also be hitched.

		public bool isStrippedServant() { return isServant () && isStripped(); }

		//the external link added to head is informally called the 'shackle'. All external bones are shackles.
		public bool isShackledStrippedServant() {
			return isStrippedServant () && head.bones.Count > 0 && head.bones [head.bones.Count - 1].otherEnd(head).org == master;
		}

		public void liberate(){ 
			if (isServant()) {
				if (isShackledStrippedServant ()) head.removeBone (head.bones.Count - 1);
				if (isStrippedServant ()) raiseGrip();
				master = null;
			}
		}

		public bool bothRegistered(Org loser){
			return Score.registered (head.id) && Score.registered (loser.head.id);
		}

		public void scheduleRelocation(Org loser) {
			if (Score.registered(head.id) && Score.registered(loser.head.id))
				Engine.scheduledOrgRelocations.Add(loser);
		}

		public void raceRelocate(){ //stage right
			float delta = Bots.worldRadius - head.x;
			foreach (var memb in members) { 
				memb.nx = memb.x += delta; 
			}
		}

		//preserves form and orientation of the organism. Called when x==nx, y==ny, and preserves that.
		public void randomRelocate(){
			Vector2 here = new Vector2(head.x, head.y);
			Vector2 delta = (Bots.worldRadius * Random.insideUnitCircle)-here;

			foreach (var memb in members) { 
				memb.nx = memb.x += delta.x; 
				memb.ny = memb.y += delta.y;
			}
		}
			
		public float oomph(){
			float sum = 0;
			foreach (var memb in members) sum += memb.oomph;
			return sum;
		}

		//reserve capacity, the amount of oomph the org could absorb in becoming completely maxd out.
		public float hunger(){
			float sum = 0;
			foreach (var memb in members) sum += memb.maxOomph - memb.oomph;
			return sum;
		}

		//factor of 0 means no change, factor of 1 means oomph is zeroed.
		public void decreaseOomph( float factor){
			if (factor > 1) factor = 1;
			if (factor < 0) factor = 0;

			foreach (var node in members) node.oomph *= (1-factor);
		}

		//factor of zero means no change, factor of 1 means hunger is sated, i.e. hunger becomes zero, every member at their maxOomph
		public void decreaseHunger( float factor){
			if (factor > 1) factor = 1;
			if (factor < 0) factor = 0;

			if (factor == 1) { foreach (var node in members) node.oomph = node.maxOomph; }//just to avoid any possible numerical overshoot below
			else foreach (var node in members) node.oomph += (node.maxOomph-node.oomph)*factor;
		}

		public void eatOomph(Org eaten){

			//take all the oomph you can
			float thisOrgCanEat = this.hunger ();
			float eatenOrgCanGive = eaten.oomph ();
			float canTransfer = Mathf.Min(thisOrgCanEat, eatenOrgCanGive);

			if (canTransfer > 0){ //guard against zeroDivide by zero thisOrgCanEat or eatenOrgCanGive
				eaten.decreaseOomph(canTransfer/eatenOrgCanGive);
				this.decreaseHunger(canTransfer/thisOrgCanEat);
			}
		}

		public Org setTeamNumber (int value){
			foreach (var node in members) node.setDna (CScommon.leftTeamBit, CScommon.rightTeamBit, value);
			return this;
		}


		public void cutOut(){
			
			liberatePrisoners(); //liberate any prisoners--those shackled to my hitch by an external bone

			liberate(); //liberate org from being a prisoner, breaking its external bone, and from being a grip-stripped servant, or even just an oomph-feeding servant

			//take out all muscles attacking me
			List<Muscle> attackers = new List<Muscle>(enemyMuscles);
			foreach (var muscl in attackers) muscl.cut ();
			if (Debug.isDebugBuild) Debug.Assert (enemyMuscles.Count == 0);

			//take out all of my external muscles
			foreach (var memb in members) { memb.cutExternalMuscles();}

		}

		public Org makeHitched(Node hitch0 = null){
			hitch = (hitch0 != null && members.Contains (hitch0)) ? hitch0 : members [members.Count - 1]; //default is last member
			return this;
		}

		public bool hasHitch(){
			return hitch != null;
		}

		public bool hasPrisoner(){
			if (hitch == null) return false;
			return prisoners ().Count > 0;
		}

		public List<Node> prisoners(){
			List<Node> prsnrs = new List<Node> ();
			if (hitch == null || hitch.bones.Count == 0) return prsnrs; //empty
			foreach (var b in hitch.bones) if (b.isExternal()) prsnrs.Add(b.otherEnd(hitch));
			return prsnrs;
		}
			
		// "prisoners" are shackledStrippedServants
		public void liberatePrisoners(){
			List<Node> prsnrs = prisoners ();
			for (int i=0; i < prsnrs.Count; i++) prsnrs[i].org.liberate ();
		}

		public void takePrisoner(Org victim){

			if (victim.members.Count > 1) return; //we're only picking up rocks
			if (!hasHitch()) return;
			//if (prisoners().Count > 0) return; //we're only picking up one rock
			victim.makeShackledStrippedServant(this);

		}


		//error to do this of a node that isn't a soliton, i.e. alone in its org
		public void makeMember(Node node){ //ancien addTruster
			if (members.Contains(node)) return;
			if (node.org.members.Count > 1) {
				if (Debug.isDebugBuild) Debug.LogError ("Inconsistent organisms!!");
				node.isolate();
			}
			node.org = this;
			members.Add(node);
		}


		public List<Node> disassemble(){

			liberatePrisoners(); //should have already been liberated, but is cheap insurance

			List<Node> bits = new List<Node> (members);

			bits.Reverse (); //so head node is last to be isolated. This is just a superstition.
			foreach (var node in bits) {
				node.isolate(); //makes each into its own separate org
			}
			return bits;
		}


		// sharing oomph via virtual 'intra org' link whose efficiency still degrades with distance,
		// to maintain locality of the material world.
		// Give from the best supplied to the worst supplied, i.e. from the least needy to the most needy
		public void shareOomph(){
			
			if (members.Count == 1) return; //nobody to share among

			Node bestSupplied = head;
			Node worstSupplied = bestSupplied;
			for (int i = 1; i < members.Count; i++) {
				if (members[i].fuelGauge < worstSupplied.fuelGauge ) worstSupplied = members[i];
				if (members[i].fuelGauge > bestSupplied.fuelGauge ) bestSupplied = members[i];
			}

			float fairfuelGauge = (worstSupplied.fuelGauge + bestSupplied.fuelGauge)/2;
			float mostWorstShouldGet = worstSupplied.maxOomph*fairfuelGauge - worstSupplied.oomph; //would raise his fuelGauge to fairfuelGauge level
			float mostBestShouldGive = bestSupplied.oomph - fairfuelGauge * bestSupplied.maxOomph; //will drop best's fuelGauge to fairfuelGauge level
			float oomphToTransfer = Mathf.Min(mostWorstShouldGet, mostBestShouldGive);
			bestSupplied.oomph -= oomphToTransfer;
			worstSupplied.oomph += oomphToTransfer*Node.linkEfficiency(bestSupplied, worstSupplied); //with love. Less will be actually transfered, because of efficiency

		}

		public float minDistance(Org other){
			float d = head.distance (other.head);
			foreach (var n in members)
				foreach (var m in other.members)
					if (n.distance (m) < d)
						d = n.distance (m);
			return d;
		}

		//center of balance
		public Vector2 COB(){
			Vector2 response = Vector2.zero;
			for (int i=0; i<members.Count; i++) {response.x += members[i].x; response.y += members[i].y;}
			return response/members.Count;
		}

		//direction from COB to least node. If members.Count == 1, returns Vector2.zero
		public float orientation(){
			Vector2 cb = COB();
			return  Mathf.Atan2(head.y-cb.y, head.x-cb.x);
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

		public void setState(string key, int value){
			states[key] = value;
		}

	}
}