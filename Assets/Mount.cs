using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bubbles {
	
	public class Mount {

		//Mounting and dismounting.

		public static bool available(int nodeId){
			return (mountable (nodeId) && !Engine.nodes [nodeId].testDna (CScommon.playerPlayingBit));
		}
		

		//returns whether nodeId head of mountable org
		public static bool mountable(int nodeId ){
			if (nodeId < 0 || nodeId >= Engine.nodes.Count) return false;
			Node node = Engine.nodes [nodeId];
			//			if (Debug.isDebugBuild) Debug.Log (nodeId + " " + node.testDna (CScommon.playerBit) + " " + node.testDna (CScommon.playerPlayingBit) +
			//				" " + (node == node.org.head) + " " + node.teamNumber + " " + node.testDna (CScommon.goalBit));
			if (!node.testDna(CScommon.playerBit)) return false;
			if (node != node.org.head) return false; //has to be org head
			if (node.teamNumber == 0) return false; // team 0 orgs can't be mounted
			if (node.testDna(CScommon.goalBit)) return false; //goals can't be mounted
			return true;
		}

		//when a player mounts a node, they get the ability to command any pushPullServo that may be on the node,
		//and they get the ability to flee/attack via targeting
		public static bool mount(int nodeId){
			if (available(nodeId))  {

				Engine.nodes[nodeId].setDna(CScommon.playerPlayingBit, true);
				pullFromTeam (nodeId);

				return true;
			}
			return false;
		}

		//returns whether or not was successful
		public static bool dismount(int nodeId){
			if (!mountable (nodeId)) return false; //couldn't have been mounted
			Node node = Engine.nodes[nodeId];
			if (!node.testDna(CScommon.playerPlayingBit)) return false; //wasn't mounted

			node.setDna(CScommon.playerPlayingBit, false);
			pushTeamIdBack (nodeId);

			return true;
		}


		public static List<Node>[] teams = new List<Node>[4]; //lists of team members (org heads), does not change after makeTeamLists sets it up.
		private static List<int>[] unmountedTeams = new List<int>[4]; //dynamic lists of ids of team members not yet mounted

		//at beginning of every round teamLists are made afresh.
		public static void makeTeamLists(){

			//if (Debug.isDebugBuild) Debug.Log ("makeTeamLists " + teams.Length);

			for (int i = 0; i < teams.Length; i++) {
				teams [i] = new List<Node> ();
				unmountedTeams [i] = new List<int> ();
			}

			for (int id=0; id < Engine.nodes.Count; id++) {
				//i == Engine.nodes [i].id;
				if (mountable(id)) { // mountables don't have teamNumber 0, so teams[0].Count is always 0 ,as is unmountedTeams[0].Count.
					//if (Debug.isDebugBuild) Debug.Log ("node "+id+" team " + Engine.nodes[id].teamNumber + ": " + teams [Engine.nodes[id].teamNumber].Count);
					teams [Engine.nodes[id].teamNumber].Add (Engine.nodes[id]); 
					unmountedTeams[Engine.nodes[id].teamNumber].Add (id);
				}
			}
			//at this point teams and unmountedTeams refer to the same nodes in the same order, the latter by node.id
		}


		//the following three use the private unmountedTeams to keep track of which team members are still available for mounting
		public static int idFromLargestTeam(){
			int longest = 0;
			for (int i=0;i<unmountedTeams.Length;i++) if (unmountedTeams[i].Count > unmountedTeams[longest].Count) longest = i;
			if (unmountedTeams [longest].Count == 0) return -1;
			return unmountedTeams [longest] [0];
		}

		public static void dumpUnmountedTeams(){
			for (int i = 0; i < unmountedTeams.Length; i++) { 
				string s = "unmounted ";
				s += unmountedTeams [i].Count + ":";
				for (int j = 0; j < unmountedTeams [i].Count; j++)
					s += " " + unmountedTeams [i] [j];
				Debug.Log (s);
			}
		}

		static void pullFromTeam (int id){
			unmountedTeams [Engine.nodes[id].teamNumber].Remove(id);
		}

		//for putting back ones that have been popped off
		static void pushTeamIdBack( int id){
			unmountedTeams [Engine.nodes[id].teamNumber].Add(id);
		}
	
	}
}

