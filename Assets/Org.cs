using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bubbles{
	public class Org {
		
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
			foreach (Node n in aNodes) if (firstNode.org != n.org) return; 
			foreach (Node n in bNodes) if (firstNode.org != n.org) return;

			foreach(Node n in aNodes) {leftovers+= n.burden - n.minBurden; n.burden = n.minBurden ;}

			//any node in both aNodes and bNodes already has its minburden, so contributes nothing further to leftovers
			foreach(Node n in bNodes) {leftovers+= n.burden - n.minBurden; n.burden = n.minBurden;}

			//what to distribute to each node
			aPart = leftovers*fraction/aNodes.Count;
			bPart = leftovers*(1.0f-fraction)/bNodes.Count;

			foreach(Node n in aNodes) n.burden += aPart;
			foreach(Node n in bNodes) n.burden += bPart; // += so any shared node gets both loads

		}


		public List<Node> members; //this has Count of at least 1, and its first member is considered the head of the org for directional purposes
		public Node head { get; private set; }
		public List<Muscle> enemyMuscles;
		
		public string clan;
		public int team;
		public Dictionary<string,int> states; //Keys are independent state dimensions.

		private Node hitch; //org doesn't exist with no members

		public Org (Node firstMember){
			members = new List<Node>();
			members.Add (firstMember);
			head = firstMember; //convenience for muscles[0]
			enemyMuscles = new List<Muscle>();
			states = new Dictionary<string,int> ();
			setRandomClan(); // not likely to be the clan of anything else in the world, nearly inevitably unique
		}

		public Org setRandomClan() {
			clan = Random.Range(0,int.MaxValue).ToString();
			return this;
		}

		public Org makeHitched(Node hitch0 = null){
			hitch = (hitch0 != null && members.Contains (hitch0)) ? hitch0 : members [members.Count - 1]; //default is last member
			return this;
		}

		public bool hasHitch(){
			return hitch != null;
		}

		//returns null if no external org hitched to hitch. Assumes tail composed of nodes of different orgs than hitch's.
		public Node firstTail(){
			//no bones, or no external bone added on top of internal bones
			return (hitch == null || hitch.bones.Count == 0 || hitch.bones [hitch.bones.Count - 1].target.org == hitch.org)?
				null: hitch.bones [hitch.bones.Count - 1].target;
		}

		//only use if hasHitch()
		public Node hitchTip(){ //tip of tail of bones beginning at hitch. Is actual hitching point
			Node nextT;
			Node priorT = hitch;
			Node t = firstTail ();
			if (t == null) return hitch; //if !hasHitch() will be null
			while ((nextT = t.otherBoneChainNeighbor(priorT)) != null){ priorT = t; t = nextT; }
			return t;
		}

		public void disassembleAndTow(Org targetOrg){
			
			if (!hasHitch()) return;

			List<Node> bits = targetOrg.disassemble ();

			hitchTip().addBone (bits [0]); //add first one to tip of tail
			for (int i= 1; i<bits.Count; i++) {
				bits [i - 1].addBone (bits [i]);
			}
		}

		//can recycle the org in first parm. If first parm null, will return a new org.
		// // // recycling is too dangerous. What happens to all the members.org? 
	//	public static Org makeOrg(Org org, Node firstMember, string clan0 = ""){
	//
	//		if (org == null)
	//			return new Org (firstMember, clan);
	//		
	//		org.members.Clear();
	//		org.enemyMuscles.Clear();
	//		org.states.Clear();
	//		org.rules.Clear();
	//
	//		org.members.Add (firstMember);
	//		org.leastNodeId = firstMember.id;
	//		org.clan = (clan0=="")?getRandomClan():clan0;
	//		return org;
	//	}


	//	public void clearTrust(){
	//		trustHead = this;
	//		foreach (Node n in trusters) n.trustHead = n;
	//		trusters.Clear ();
	//	}

		//breaking this org into independent nodes having loner (members.Count == 1) organisms
		//state preserved, in this org and in all new orgs the same
		// What To Do With EnemyMuscles??? Presume org has been cut out? Need to cut out all the new organisms, so that nobody's targeting them, and their enemyMuscles is empty
	//	public void shatter(){
	//		for (int i = 1; i < members.Count; i++) { //head unaffected
	//			members [i].org = new Org (members [i].org);
	//			members [i].org.states = new Dictionary<string,int> (states); // ???
	//		}
	//		members.Clear ();
	//		members.Add (head);
	//	}

		//error to do this of a node that isn't alone in its org
		public void makeMember(Node node){ //ancien addTruster
			if (members.Contains(node)) return;
			if (node.org.members.Count > 1) {
				Debug.LogError ("Inconsistent organisms!!");
				node.isolate();
			}
			node.org = this;
			members.Add(node);
		}


		public List<Node> disassemble(){
			List<Node> bits = new List<Node> (members);
			foreach (var node in bits) {
				node.isolate(); //makes each into its own separate org of team 0 and independent clan
			}
			return bits;
		}

		//set of nodes that trust each other to shift burden and share oomph and make bones.
		public List<Node> trustGroup()
		{	return new List<Node>(members);
		}


		// sharing oomph via virtual 'intra org' link whose efficiency still degrades with distance,
		// to maintain locality of the material world.
		// Give from the best supplied to the worst supplied, i.e. from the least needy to the most needy
		public void shareOomph(){
			
			if (members.Count == 1) return; //nobody to share among

			Node bestSupplied = head;
			Node worstSupplied = bestSupplied;
			for (int i = 1; i < members.Count; i++) {
				if (members[i].supply < worstSupplied.supply ) worstSupplied = members[i];
				if (members[i].supply > bestSupplied.supply ) bestSupplied = members[i];
			}

			float fairSupply = (worstSupplied.supply + bestSupplied.supply)/2;
			float mostWorstCanUse = worstSupplied.maxOomph - worstSupplied.oomph;
			float mostBestShouldGive = bestSupplied.oomph - fairSupply * bestSupplied.maxOomph; //will drop best's supply to fairSupply level
			float oomphToTransfer = Mathf.Min(mostWorstCanUse, mostBestShouldGive);
			bestSupplied.oomph -= oomphToTransfer;
			worstSupplied.oomph += oomphToTransfer*Node.linkEfficiency(bestSupplied, worstSupplied); //with love. Less will be actually transfered, because of efficiency

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