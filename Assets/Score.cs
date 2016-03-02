//copyright 2016 John Fairfield

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bubbles{

	public class Score{

		public static bool countCoup; // true: players get score when they eat another player
		public static bool hasTeams;  // true: two teams, false, no teams

		public static int[] teamScores = new int[3]; // only use 1,2. team 0 means no team

		public static void newGame(){
			for (int i = 0; i < teamScores.Length; i++)
				teamScores [i] = 0;
			newRound ();
		}

		public static void newRound(){
			foreach (var nodeId in nodeIdPlayerInfo.Keys) {
				nodeIdPlayerInfo [nodeId].data.productivity = 0;
			}
		}

		public static int teamNumber(int nodeId){
			if (nodeId >= 0) return Engine.nodes [nodeId].teamNumber; //may be 0 indicating no team
			else return 0; //no team
		}
		
		public class PlayerInfo {
			public int connectionId;
			public string name;
			public CScommon.PerformanceMsg data;

			public PlayerInfo(){
				connectionId = -1;
				name = "";
				data = new CScommon.PerformanceMsg();
				data.nodeId = -1;
				clearScore();
			}

			public void clearScore(){ data.productivity = 0; data.level = 0; }
		}




		//NOTE: some players (NPC's like snarks) may be in nodeIdPlayerInfo but they are not "connected" and so are not in connectionIdPlayerInfo.
		//Some players (connected spectators) may be in connectionIdPlayerInfo but they are not associated with any node, so are not in nodeIdPlayerInfo.
		public static Dictionary<int, PlayerInfo> nodeIdPlayerInfo = new Dictionary<int,PlayerInfo>();

		public static bool registered(int nodeId) {return nodeIdPlayerInfo.ContainsKey (nodeId);}


		public static void registerNPC(int nodeId, string name){

			if (bubbleServer.newRound) return; //suppress re-registrations between rounds

			nodeIdPlayerInfo[nodeId] = new PlayerInfo();
			nodeIdPlayerInfo[nodeId].data.nodeId = nodeId;
			nodeIdPlayerInfo[nodeId].name = name;
			//if (Debug.isDebugBuild) Debug.Log ("registerNPC " + nodeId + " " + name + " " + " is " + (nodeIdPlayerInfo.Count - 1) + "th.");
		}

			

//		public static void score(int nodeId, byte neither0Winner1Loser2){
//			if (nodeIdPlayerInfo.ContainsKey(nodeId)){
//				if (neither0Winner1Loser2==1) nodeIdPlayerInfo[nodeId].data.plus += 1;
//				if (neither0Winner1Loser2==2) nodeIdPlayerInfo[nodeId].data.minus += 1;
//				nodeIdPlayerInfo[nodeId].data.neither0Winner1Loser2 = neither0Winner1Loser2;
//
//				int change = neither0Winner1Loser2==0?0:neither0Winner1Loser2==1?1:-1;
//				long nowMs = gameStopwatch.ElapsedMilliseconds;
//				if (Bots.countCoup){
//					long deltaMs = nowMs - nodeIdPlayerInfo[nodeId].data.gameMilliseconds;
//					nodeIdPlayerInfo[nodeId].data.performance *= Mathf.Pow(2,-deltaMs/CScommon.performanceHalfLifeMilliseconds);
//					nodeIdPlayerInfo[nodeId].data.performance += change;
//				}
//				nodeIdPlayerInfo[nodeId].data.gameMilliseconds = nowMs;
//
//				scheduledScores[nodeId] = true;
//			}
//		}

		public static List<int> teamNodeIds(int teamNum){
			List<int> tids = new List<int> ();
			foreach (var nodeId in nodeIdPlayerInfo.Keys) if (teamNumber(nodeId) == teamNum) tids.Add(nodeId);
			return tids;
		}

		public static void scoreTeamWin(int teamNum){
			
			teamScores [teamNum] += 1;

			List<int> team1 = teamNodeIds (1);
			List<int> team2 = teamNodeIds (2);

			float sum = 0;
			foreach (var nid in team1) sum += nodeIdPlayerInfo [nid].data.productivity;
			foreach (var nid in team2) sum += nodeIdPlayerInfo [nid].data.productivity;

			foreach (var nid in team1) {
				nodeIdPlayerInfo [nid].data.level += nodeIdPlayerInfo [nid].data.productivity / (1 + sum);
			}

			foreach (var nid in team2) {
				nodeIdPlayerInfo [nid].data.level += nodeIdPlayerInfo [nid].data.productivity / (1 + sum);
			}

			List<int> bonusTeam = teamNum == 1 ? team1 : team2;
			foreach (var nid in bonusTeam) {
				nodeIdPlayerInfo [nid].data.level += 0.1f * Mathf.Sqrt (sum);
			}

		}

		public static void scoreTeamLoss(int teamNum){
			if (teamNum == 1)
				scoreTeamWin (2);
			else if (teamNum == 2)
				scoreTeamWin (1);
		}

		//can be called with a negative amount to debit productivity
		public static void addToProductivity(int nodeId, float amount){
			if (nodeIdPlayerInfo.ContainsKey (nodeId)) {
				nodeIdPlayerInfo [nodeId].data.productivity += amount;
				bubbleServer.scheduledScores [nodeId] = true;
			}
		}

		public static void scoreCoup(int nodeId){ //when given node gets credit for eating
			if ( countCoup && nodeIdPlayerInfo.ContainsKey(nodeId)){
				nodeIdPlayerInfo [nodeId].data.productivity += 1;
				bubbleServer.scheduledScores [nodeId] = true;
			}
		}

	}
}

