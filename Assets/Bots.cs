//version 004

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Bots
{

	public static float norm, abnormScale;
	public static string gameName = "";
	public static int popcorn;

	private static float abnorm { get { return norm * abnormScale; } }

	//bot/organism construction

	public static Bub.Node pushVegNode(Vector2 position, float radius = 1.0f, string clan="")
	{	int id = Engine.nodes.Count;
		Bub.Node anode = new Bub.Node( id, position.x,position.y, radius).setDna(CScommon.vegetableBit, true);
		if (clan.Length > 0) anode.setClan(clan);
		Engine.nodes.Add (anode);
		return anode; // == Engine.nodes[id];
	}

	public static Bub.Node plantRandomVeg(float normalBubRadius, string clan="")
	{	return pushVegNode( Bub.worldRadius * Random.insideUnitCircle,normalBubRadius, clan);
	}

	public static Vector2 randomRectPosition(float heightProportionToWidth){
		return new Vector2 (
			Random.Range (-Bub.worldRadius, Bub.worldRadius),
			Random.Range (-heightProportionToWidth*Bub.worldRadius, heightProportionToWidth*Bub.worldRadius)
			);
	}

	public static Bub.Node plantRectRandomVeg(float normalBubRadius, float heightProportionToWidth, string clan="")
	{	
		return pushVegNode( randomRectPosition(heightProportionToWidth),normalBubRadius, clan);
	}
	

	public static Bub.Node spawnInchworm(Vector2 headPosition, float headRadius, bool headVeg, 
	                                 Vector2 tailDelta, float tailRadius, bool tailVeg, string clan=""){

		// mimic human or npc behavior building the scaffolding, i.e. establishing nodes and muscles and bones
		Bub.Node head = pushVegNode( headPosition, headRadius, clan).setDna(CScommon.vegetableBit, headVeg);
		Bub.Node tail = pushVegNode( headPosition+tailDelta, tailRadius, clan).setDna(CScommon.vegetableBit, tailVeg);
		tail.trust(head);

		List<Bub.Node> tailList = Rules.nodeList(tail);
		Rules.Push1Pull2Servo.install(head,tailList);
		Rules.NearFarPush1Pull2Cmdr.install(head, tailList, tailDelta.magnitude*0.2f,tailDelta.magnitude);
		return head;
	}
		

	public static Bub.Node spawnTricycle(Vector2 headPosition, float headRadius, bool headVegetable, 
	                                          Vector2 tailsMidpointDelta, float widthBetweenTails, float tailsRadius, bool tailsVegetable, 
	                                          string clan=""){
		
		// mimic human or npc behavior building the scaffolding, i.e. establishing nodes and muscles and bones
		Bub.Node head = pushVegNode( headPosition, headRadius, clan).setDna(CScommon.vegetableBit, headVegetable);
		
		float angle = Mathf.Atan2(tailsMidpointDelta.y, tailsMidpointDelta.x); //angle from tailsMidpoint towards head
		angle += Mathf.PI/2; // add 90 degrees, to point from tailsMidpoint to left tail
		Vector2 delta = new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(widthBetweenTails/2);
		
		Bub.Node tailL = pushVegNode( headPosition+tailsMidpointDelta+delta,tailsRadius, clan).setDna(CScommon.vegetableBit, tailsVegetable);
		Bub.Node tailR = pushVegNode( headPosition+tailsMidpointDelta-delta,tailsRadius, clan).setDna(CScommon.vegetableBit, tailsVegetable);
		
		tailL.trust(head);
		tailR.trust(head);

		tailL.addBone(tailR);

		List<Bub.Node> tailList = Rules.nodeList( tailL, tailR );
		Rules.Push1Pull2Servo.install(head,tailList);
		Rules.TurnServo.install(head,tailL,tailR);

		// make equilateral triangle at near end
		Rules.NearFarPush1Pull2Cmdr.install(head,tailList, 0.75f*widthBetweenTails ,1.4f*widthBetweenTails);

		return head;
	}


	public static float wanderAngle(float wander, float angle){
		wander = Mathf.Abs (wander);
		return angle + Random.Range (-wander, wander);
	}

	public static Bub.Node spawnTapeworm(Vector2 headPosition, bool headVeg, 
	                              int numSegments, float radius, bool tailVeg , string clan = ""){

		float randomAngle = Random.Range(-Mathf.PI, Mathf.PI);
		Vector2 tailDelta,tailPosition;

		Bub.Node head = pushVegNode( headPosition, radius, clan).setDna(CScommon.vegetableBit, headVeg);

		tailPosition = headPosition;
		Bub.Node tail = head;
		for (int n=0; n<numSegments; n++){

			Bub.Node priorTail = tail;
			randomAngle = wanderAngle(0.7f,randomAngle);
			tailDelta = 6.5f*(new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle)));
			tailPosition = tailPosition + tailDelta;
			tail = pushVegNode(tailPosition, radius, clan).setDna(CScommon.vegetableBit, tailVeg);
			tail.trust (priorTail);

			//Rules.SegmentPushPullServo.install(priorTail,tail, n);
			//Rules.NearFarPush1Pull2Cmdr.install(priorTail,Rules.nodeList(tail), 3,10);//6.5 is avg of 3 and 10...
			Rules.SegmentPulltokenServo.install(priorTail,tail);
		}

		//make final tail tiny, so is light to pull, since that weight won't be shifted off it
		tail.setRadius(radius/5);

		return head;
	}

	public static Bub.Node spawnRandomInchworm(float approximateRadius, bool headVeg, bool tailVeg, string clan=""){
		Vector2 headPosition = Bub.worldRadius * Random.insideUnitCircle;
		float randomAngle = Random.Range(-Mathf.PI, Mathf.PI);
		Vector2 tailDelta = Random.Range(2.3f,2.5f)*approximateRadius*(new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle))) ;
		float headRadius = approximateRadius * Random.Range(0.9f,0.1f);
		float tailRadius = headRadius*Random.Range(1.3f,1.5f);
		return spawnInchworm(headPosition,headRadius,headVeg,tailDelta,tailRadius,tailVeg, clan);
	}

	public static Bub.Node spawnRandomTricycle(float approximateRadius, bool headVeg, bool tailVeg, string clan=""){
		Vector2 headPosition = Bub.worldRadius * Random.insideUnitCircle;
		float randomAngle = Random.Range(-Mathf.PI, Mathf.PI);
		Vector2 tailDelta = 2.4f*approximateRadius*(new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle))) ;
		float headRadius = approximateRadius * Random.Range(0.9f,0.1f);
		float tailRadius = headRadius*Random.Range(0.7f,0.8f);
		return spawnTricycle(headPosition,headRadius,headVeg,tailDelta,tailRadius*Random.Range(1.9f,2.1f),tailRadius,tailVeg, clan);
	}

	public static Bub.Node spawnRandomTapeworm(float approximateRadius, bool headVeg, bool tailVeg,int numSegments = 0, string clan="" ){
		Vector2 headPosition = Bub.worldRadius * Random.insideUnitCircle;
		if (numSegments == 0) numSegments = Random.Range(2,7);
		return spawnTapeworm(headPosition,headVeg,numSegments,approximateRadius*Random.Range(0.9f,0.111111f),tailVeg, clan);
	}





	////////Event handlers called from bubbleServer

	public static void onTarget(int sourceId, int targetId, CScommon.LinkType linkType, byte hand){
		if (sourceId >= 0 && sourceId < Engine.nodes.Count && targetId >= 0 && targetId < Engine.nodes.Count
		    && linkType != CScommon.LinkType.bone) {

			int targetIdp1 = targetId + 1;
			if (linkType == CScommon.LinkType.puller) targetIdp1 = -targetIdp1;

			Engine.nodes[sourceId].setState(hand+"targetIdp1", targetIdp1); // push+, pull-, (targetId +1). 0 means stop fighting

		}
	}
/*
	public static void onXTarget(int sourceId, int targetId, CScommon.LinkType linkType){
		if (sourceId >= 0 && sourceId < Engine.nodes.Count && targetId >= 0 && targetId < Engine.nodes.Count
		    && mounts.ContainsKey(sourceId) && linkType != CScommon.LinkType.bone) {

			Bub.Node source = Engine.nodes[sourceId]; 
			int push1Pull2 = source.getState("push1Pull2");
			if (push1Pull2 == int.MinValue) push1Pull2 = 2;

			source.setState ("nearFarSwitch01", 0); //suppres nearFar cmdr

			source.setState ("push1Pull2",push1Pull2==1?2:1); //toggle pushPull

			Bub.Muscle muscle = mounts[sourceId]; //may be null
			//protect from linking to a part of same organism. In fact, use this as a means of deleting the muscle
			if (source.trusts(Engine.nodes[targetId])) {
				source.removeMuscle(muscle); //does nothing if null
				mounts[sourceId] = null;
			} 
			else {
				if (muscle == null) muscle = mounts[sourceId] = source.addMuscle(Engine.nodes[targetId]);
				muscle.target = Engine.nodes[targetId];
				if (linkType == CScommon.LinkType.pusher) muscle.makePusher(); 
				else if (linkType == CScommon.LinkType.puller) muscle.makePuller();
			}
		}
	}
*/

	public static void togglePushPull(Bub.Node source){
		int push1Pull2 = source.getState ("push1Pull2");
		if (push1Pull2 == 2) source.setState ("push1Pull2",1);
		else if (push1Pull2 == 1) source.setState ("push1Pull2",2); 
	}

	//manual push/pull 1:push, 2:Pull, 3. togglePushPull, 0. return to automatic pushPull
//	public static void onPush1Pull2(int sourceId, int push1Pull2){
//	
//		switch (push1Pull2) {
//		
//			case 0:  
//				Engine.nodes[sourceId].setState ("nearFarSwitch01", 1); //enable automatic nearFar cmdr
//				break;
//			case 1:
//			case 2:
//				Engine.nodes[sourceId].setState ("nearFarSwitch01", 0); //disable automatic cmdr
//				Engine.nodes[sourceId].setState ("push1Pull2",push1Pull2); 
//				break;
//			case 3: 
//				Engine.nodes[sourceId].setState ("nearFarSwitch01", 0); //disable automatic cmdr
//				togglePushPull(Engine.nodes[sourceId]);
//				break;
//			}
//	}

	//reversal effect: push pull servo shifts burden to determine which end moves more during push bzw pull
	//0 means forward, 1 means reverse. Anything else means toggle.
	//subsumed by "onSpeed"
//	public static void onForward0Reverse1(int sourceId, int forward0Reverse1){
//		int pre = Engine.nodes[sourceId].getState("forward0Reverse1");
//
//		if (forward0Reverse1 == 0 || forward0Reverse1 == 1)	Engine.nodes[sourceId].setState("forward0Reverse1",forward0Reverse1);
//		else Engine.nodes[sourceId].setState("forward0Reverse1",Engine.nodes[sourceId].checkState("forward0Reverse1",1)?0:1);
//		
//		//compare pre to post
//		if (pre != Engine.nodes[sourceId].getState("forward0Reverse1")) togglePushPull(Engine.nodes[sourceId]); //so has immediate effect
//
//		//if (UnityEngine.Debug.isDebugBuild) bubbleServer.debugDisplay ("fr "+forward0Reverse1+" "+Engine.nodes[sourceId].getState ("forward0Reverse1"));
//	}

	//changes the metabolic rate (power) of internal muscles of the given mount
	public static void onSpeed(Bub.Node mount, int speed){
		int absSpeed = Mathf.Abs (speed);
		List<Bub.Node> orgNodes = mount.trustGroup ();

		foreach (var node in orgNodes) {
			int forward0Reverse1 = node.getState ("forward0Reverse1");

			if (forward0Reverse1 == 0 || forward0Reverse1 == 1) {
				//so effect is immediate
				if (forward0Reverse1 == 0 && speed < 0)
					togglePushPull (node);
				if (forward0Reverse1 == 1 && speed > 0)
					togglePushPull (node);
			
				//so effect stands
				if (speed < 0)
					node.setState ("forward0Reverse1", 1);
				else
					node.setState ("forward0Reverse1", 0);
				node.enableInternalMuscles (absSpeed);
			}
		}
	}

	public static void onTurn(int sourceId, int turn){
		if (turn >= -1 && turn <= 1) Engine.nodes[sourceId].setState("turn",turn);
	}



	//Mounting and dismounting.

	//returns whether or not there was anything to dismount
	public static bool dismount(int nodeId){
		if (nodeId < 0 || nodeId >= Engine.nodes.Count) return false;
		Bub.Node node = Engine.nodes [nodeId];
		if (!node.testDna(CScommon.playerPlayingBit)) return false;
		if (node != node.trustHead) return false; //can only do orgs

		node.setDna(CScommon.playerPlayingBit, false);
		pushTeamOrgBack (nodeId);

		return true;
	}

	//returns whether nodeId is mountable org
	public static bool mountable(int nodeId ){
		if (nodeId < 0 || nodeId >= Engine.nodes.Count) return false;
		Bub.Node node = Engine.nodes [nodeId];
		if (!node.testDna(CScommon.playerBit)) return false;
		if (node.testDna(CScommon.playerPlayingBit)) return false; //already mounted
		if (node != node.trustHead) return false; //has to be org
		if (!teams[node.getTeam()].Contains(nodeId)) return false; // team 0 orgs can't be mounted
		return true;
	}

	//when a player mounts a node, they get the ability to command any pushPullServo that may be on the node,
	//and they get the ability to flee/attack via targeting
	public static bool mount(int nodeId){
		if (mountable(nodeId))  {
			
			Engine.nodes[nodeId].setDna(CScommon.playerPlayingBit, true);
			pullOrgFromTeam (nodeId);

			return true;
		}
		return false;
	}

	public static int mountOrgFromLargestTeam(){
		int nodeId = orgFromLargestTeam();
		if (mount (nodeId)) return nodeId;
		bubbleServer.debugDisplay ("no NPCs available");
		return -1;
	}


	static List<int>[] teams = new List<int>[4]; //lists of ids of heads of all team member orgs

	static void makeTeams(){

		for (int i = 0; i<teams.Length; i++) teams[i] = new List<int>();

		for (int i = 0; i < Engine.nodes.Count; i++) if (Engine.nodes[i].trustHead == Engine.nodes[i]) {
				int t = Engine.nodes [i].getTeam ();
				if (t > 0) {//don't bother with team 0, is most of nodes, is "no team".
					teams [t].Add(i); 
					if (!mountable(i)) bubbleServer.debugDisplay("Error unmountable org added to team."); //don't check team, you're building team.
				}
			}
	}
		

	public static int orgFromLargestTeam(){
		int longest = 0;
		for (int i=0;i<teams.Length;i++) if (teams[i].Count > teams[longest].Count) longest = i;

		if (teams [longest].Count == 0) return -1;
		return teams[longest][0];
	}

	static void pullOrgFromTeam (int id){
		int t = Engine.nodes [id].getTeam ();
		teams [t].Remove (id);
	}

	//for putting back ones that have been popped off
	static void pushTeamOrgBack( int id){
		int t = Engine.nodes [id].getTeam ();
		teams [t].Add(id);
	}


	// initialization

	public static void initialize(int gameNumber){
		
		switch (gameNumber) {
		case 1:
			gameName = "snark";
			snarkInit ();
			break;
		case 2:
			gameName = "race";
			inchwormRaceInit ();
			break;
		case 3:
			gameName = "fussball";
			fussballInit ();
			break;
		case 4:
			gameName = "turm";
			turmInit ();
			break;
		case 5:
			gameName = "turm2";
			turmInit2 ();
			break;
		default: 
			gameName = "sizeTest";
			testbedInit ();
			break;
		}

		makeTeams ();
	}


	//only specify goal if all PC's, as NPC's, should seek the same goal. Otherwise, add Autopilot individually
//	private static void installAllPCRules(Bub.Node goal = null){
//		for (int i = 0; i< Engine.nodes.Count; i++) if (Engine.nodes[i].testDna(CScommon.playerBit)){
//				Bub.Node mountable = Engine.nodes [i];
//				if (mountable.getTeam()==0)bubbleServer.debugDisplay ("Bots Error: unteamed mountable node " + i);
//				Rules.Autopilot.install(mountable,goal); //does nothing if goal == null
//				Rules.HunterNPCRule.install(mountable);
//				Rules.HunterPCRule.install(mountable,0);
//				Rules.HunterPCRule.install(mountable,1);
//				if (!bubbleServer.registered (mountable.id))
//					bubbleServer.registerNPC (mountable.id, "B" + mountable.id + "T");
//			}
//	}

	private static Bub.Node setUpPlayer(Bub.Node head, int teamNumber,  string name, Bub.Node goal = null, int internalSpeed = 100){

		//onSpeed(head,internalSpeed); 

		//note that team number 0 is unscored, i.e. indicates no team at all, singleton
		head.setTeam(teamNumber);
		head.setDna(CScommon.playerBit,true); //ie is mountable

		//Rules.Autopilot.install(head,goal); //does nothing if goal is null
		Rules.HunterNPCRule.install(head);
		Rules.HunterPCRule.install(head,0);
		Rules.HunterPCRule.install(head,1);

		if (name == "") name = "B"+head.id+"T";
		bubbleServer.registerNPC(head.id, name);

		return head;
	}


	public static List<Bub.Node> spawnRandomTeam(float siz, int inchworms, int tricycles, int tapeworms, int teamNumber, string clan = "", Bub.Node goal = null, int internalSpeed = 100){
		List<Bub.Node> players = new List<Bub.Node>();

		for (int i = 0; i< inchworms; i++){
			players.Add(setUpPlayer(spawnRandomInchworm(siz,false,false,clan),  teamNumber, "", goal, internalSpeed));
		}

		for (int i=0; i<tricycles; i++){
			players.Add(setUpPlayer(spawnRandomTricycle(siz,false,false,clan),  teamNumber, "", goal, internalSpeed));
		}

		for (int i=0; i<tapeworms; i++){
			players.Add(setUpPlayer(spawnRandomTapeworm(siz,false,false,5,clan),teamNumber, "", goal, internalSpeed));
		}

		return players;
	}
		
	public static void snarkInit(){
		Bub.Node head;
		Bub.Node bs,ls;

//		head = spawnTapeworm(new Vector2(100,100),false, 7, abnorm*0.8f, false ,"snark");
//		setUpPlayer (head, 1, "tapeworm");
//		head.setDna (CScommon.snarkBit,true);

		head = spawnInchworm(new Vector2(-3,-3), abnorm*1.2f, false, 
			new Vector2(7,7), abnorm*1f, false,"snark"); 
		setUpPlayer (head, 0, "big snark");
		head.setDna (CScommon.snarkBit,true);
		bs = head;

		head = spawnInchworm(new Vector2(0,10),abnorm*0.6f,false,
			new Vector2(8,0), abnorm*0.5f,false,"snark");
		setUpPlayer (head, 0, "lil snark");
		head.setDna (CScommon.snarkBit,true);
		ls = head;

		for (int i=0; i<popcorn; i++) plantRandomVeg(Random.Range(0.22f, 0.9f)*Random.Range(0.22f, 0.9f)*norm); //random clans

		for (int i = 0; i<popcorn/3; i++) {
			head = spawnRandomInchworm(norm*Random.Range (0.48f,0.52f),true,true,"popcorn");
			Rules.Autopilot.install(head,ls);
		}

//		spawnRandomTeam(abnorm,2,2,1,2,"people",bs);
//		spawnRandomTeam(abnorm,2,2,1,2,"people",ls);

		spawnRandomTeam (abnorm, 8, 0, 0, 1, "", null, 50);

	}


	private static float offset(int i){ return abnorm*i; }


	public static void inchwormRaceInit(){
		Bub.Node head, goal;

		goal = pushVegNode(new Vector2(-Bub.worldRadius,0),norm*5, "goal"); //goal left, won't eat anybody
		

		head = spawnTricycle(new Vector2( Bub.worldRadius,offset(5))    , abnorm*1, false,
			new Vector2(2,0), 1.4f, abnorm*0.75f, false, "t1"); //nodes 1,2,3
		setUpPlayer(head,1,"Al",goal);

		head = spawnInchworm(new Vector2( Bub.worldRadius,offset(8))    , abnorm*1.2f, false,
			new Vector2(2,0), abnorm*1f, false,"t1"); //nodes 4,5
		setUpPlayer(head,1,"Beth",goal);
		
		head = spawnTricycle(new Vector2( Bub.worldRadius,offset(11))    , abnorm*1, false,
			new Vector2(2,0), 1.4f, abnorm*0.75f, false, "t1"); //nodes 6,7,8
		setUpPlayer(head,1,"Carl",goal);

		head = spawnInchworm(new Vector2( Bub.worldRadius,offset(14))    , abnorm*1.2f, false,
			new Vector2(2,0), abnorm*1f, false,"t1"); //nodes 9,10
		setUpPlayer(head,1,"Dee",goal);

		
		head = spawnTricycle(new Vector2( Bub.worldRadius,-offset(5))   , abnorm*1, false,
			new Vector2(2,0), 1.4f, abnorm*0.75f, false, "t2"); //nodes 11.12.13
		setUpPlayer(head,2,"Emily",goal);

		head = spawnInchworm(new Vector2( Bub.worldRadius,-offset(8))   , abnorm*1.2f, false,
			new Vector2(2,0), abnorm*1f, false,"t2"); //nodes 14,15
		setUpPlayer(head,2,"Fred",goal);

		head = spawnTricycle(new Vector2( Bub.worldRadius,-offset(11))   , abnorm*1, false,
			new Vector2(2,0), 1.4f, abnorm*0.75f, false, "t2"); //nodes 16,17,18
		setUpPlayer(head,2,"Grace",goal);

		head = spawnInchworm(new Vector2( Bub.worldRadius,-offset(14))   , abnorm*1.2f, false,
			new Vector2(2,0), abnorm*1f, false,"t2"); //nodes 19,20
		setUpPlayer(head,2,"Hank",goal);

		for (int i=0; i<popcorn; i++) pushVegNode( randomRectPosition(0.17f),Random.Range(0.5f, 2.0f)*norm); //random clans
		
		for (int i = 0; i < 7; i++) {
			head = spawnRandomInchworm (abnorm * Random.Range (0.6f, 0.7f), false, false, "pest");
			foreach (var node in head.trustGroup()) { node.ny = node.y /= 5; node.nx = node.x /= 2; } //center these somewhat
			Rules.HunterNPCRule.install(head);
		}

	}


	public static void fussballInit(){

		float rad = 12; // turn radius is half of turm side length
		float height = Mathf.Sqrt((rad*2)*(rad*2)-rad*rad); //height of equilateral triangle of sides = 2 rad
		float centerHeight = rad*rad/height; //height of center of that triangle
		float radius = height - centerHeight; //radius of circle circumscribed about three vertices
		Bub.Node crankA, crankB, crankC, one, two, three, center;

		//make triangle to swing the crank. 
		center = pushVegNode(new Vector2(0,0),0.7f*norm,"crank");
		center.setDna (CScommon.snarkBit, true);
		bubbleServer.registerNPC(center.id,"fussball");

		one = pushVegNode(new Vector2(-rad,-centerHeight),norm,"crank");
		two = pushVegNode(new Vector2(rad,-centerHeight),norm,"crank");
		three = pushVegNode(new Vector2(0,radius),norm,"crank");

		//cranks are bigger, generate most power for the whole org
		crankA = pushVegNode(new Vector2(-rad,centerHeight),norm*3,"crank");
		crankB = pushVegNode(new Vector2(rad,centerHeight),norm*3,"crank");
		crankC = pushVegNode(new Vector2(0,-radius),norm*3,"crank");
		
		one.trust(center); two.trust(center); three.trust(center); 
		crankA.trust(center);crankB.trust(center);crankC.trust(center);
		
		//can add bones only after they trust each other, i.e. after organism has been created.
		one.addBone(two); two.addBone(three); three.addBone(one);
		one.addBone(center); two.addBone(center); three.addBone(center);
		center.addBone(crankA);center.addBone(crankB); center.addBone(crankC);
		crankA.addBone(crankB);crankB.addBone(crankC);crankC.addBone(crankA);
		//put all weight in the support triangle
		Bub.shiftBurden(0,Rules.nodeList(crankA,crankB,crankC),Rules.nodeList(one,two,three));

		Rules.TurmDefender.install(crankA,5*rad);
		Rules.TurmDefender.install(crankB,5*rad);
		Rules.TurmDefender.install(crankC,5*rad);

		Rules.TurmDefender.install(one,3*rad);
		Rules.TurmDefender.install(two,3*rad);
		Rules.TurmDefender.install(three,3*rad);

		Rules.Crank.install(one,center,crankA, true); 
		Rules.Crank.install(two,center, crankA, true);
		Rules.Crank.install(three,center,crankA, true);

		Rules.Crank.install(one,center,crankB, true); 
		Rules.Crank.install(two,center, crankB, true);
		Rules.Crank.install(three,center,crankB, true);

		Rules.Crank.install(one,center,crankC, true); 
		Rules.Crank.install(two,center, crankC, true);
		Rules.Crank.install(three,center,crankC, true);

		for (int i=0; i<(popcorn*3)/4; i++) plantRandomVeg(Random.Range(0.22f, 0.9f)*Random.Range(0.22f, 0.9f)*norm); //random clans
		for (int i = 0; i<popcorn/4; i++) {
			Rules.Autopilot.install(spawnRandomInchworm(norm*Random.Range (0.48f,0.52f),true,true,"popcorn"),center);
		}

		spawnRandomTeam (abnorm, 2, 2, 0, 1, "shirts", center);
		spawnRandomTeam (abnorm, 2, 2, 0, 2, "skins", center);


	}
		

	//plants a turm of the given radius at the given position z
	public static Bub.Node plantTurm(Vector2 z, float rad){
		// turm radius is half of turm side length
		float height = Mathf.Sqrt((rad*2)*(rad*2)-rad*rad); //height of equilateral triangle of sides = 2 rad
		float centerHeight = rad*rad/height; //height of center of that triangle
		Bub.Node one, two, three, goal, feeder1, feeder2, feeder3;
		float norm = abnorm;

		//not quite on center, so pushing of links within turm will be unstable, so hopefully turm will purge itself of interlopers
		goal = pushVegNode(z+new Vector2(0.01f,-0.0223f),norm/4).setClan("turm").setDna(CScommon.noPhotoBit, true);
		bubbleServer.registerNPC(goal.id,"goal");

		one = pushVegNode(z+new Vector2(rad,-centerHeight),3*norm,"turm").setDna(CScommon.noPhotoBit, true).setDna(CScommon.vegetableBit, false);
		two = pushVegNode(z+new Vector2(-rad,-centerHeight),3*norm,"turm").setDna(CScommon.noPhotoBit, true);
		three = pushVegNode(z+new Vector2(0,height-centerHeight),3*norm,"turm").setDna(CScommon.noPhotoBit, true);

		Rules.TurmDefender.install(one,5*rad); 
		Rules.TurmDefender.install(two,5*rad); 
		Rules.TurmDefender.install(three,5*rad);
		Rules.TurmDefender.install(goal,5*rad);

		//feeders are close
		feeder1 = pushVegNode(z+new Vector2(0.93f*rad,0.22f*rad),1.9f*norm,"turm").setDna (CScommon.vegetableBit, false);
		feeder2 = pushVegNode(z+new Vector2(-1.2f*rad,-0.8f*rad),3.1f*norm,"turm").setDna (CScommon.vegetableBit, false);
		feeder3 = pushVegNode(z+new Vector2(0.83f*rad,-0.38f*rad),4.3f*norm,"turm").setDna (CScommon.vegetableBit, false);

		one.trust(goal); two.trust(goal); three.trust(goal); feeder1.trust(goal); feeder2.trust(goal); feeder3.trust(goal);

		//can add bones only after they trust each other, i.e. after organism has been created.
		one.addBone(two); two.addBone(three); three.addBone(one); 
		goal.addBone(one); goal.addBone(two); goal.addBone(three);

		//Weakness: Makes turm points heavier, but makes feeders easy to steal. Take them far far away and turm might starve.
		feeder1.giveBurden (one);
		feeder2.giveBurden (two);
		feeder3.giveBurden (three);

		return goal;
	}


	public static void turmInit(){
		
		Bub.Node goal1 = plantTurm(new Vector2(-150, 30), 12);
		Bub.Node goal2 = plantTurm(new Vector2( 150,-30), 12);

		//munchies. At less than fully enabled, they should accumulate some oomph
		Bub.Node head;
		for (int i = 0; i<popcorn/3; i++) {
			head = spawnRandomInchworm(norm*Random.Range (0.7f,0.8f),true,true,"popcorn");
			head.enableInternalMuscles(50);
		}
		for (int i = 0; i<popcorn/3; i++) {
			head = spawnRandomInchworm(norm*Random.Range (0.7f,0.8f),true,true,"popcorn");
			head.enableInternalMuscles(50);
			Rules.Autopilot.install(head,goal1); 
		}
		for (int i = 0; i<popcorn/3; i++) {
			head = spawnRandomInchworm(norm*Random.Range (0.7f,0.8f),true,true,"popcorn");
			head.enableInternalMuscles(50);
			Rules.Autopilot.install(head,goal2);
		}

		spawnRandomTeam(abnorm, 3, 3, 1, 1, "shirts", goal2);
		spawnRandomTeam(abnorm, 3, 3, 1, 2, "skins" , goal1);

	}

	//***
	public static void turmInit2(){
		float rad = 30f; // turn radius is half of turm side length
		float height = Mathf.Sqrt((rad*2)*(rad*2)-rad*rad); //height of equilateral triangle of sides = 2 rad
		float centerHeight = rad*rad/height; //height of center of that triangle
		Bub.Node one, two, three, goal, feeder1, feeder2, feeder3;
		Bub.Node one1, two1, three1, goal1, feeder11, feeder21, feeder31;
		
		goal = pushVegNode(new Vector2(Bub.worldRadius/2f, Bub.worldRadius/4f),norm/4).setClan("turm");
		bubbleServer.registerNPC(goal.id,"goal");
		
		one = pushVegNode(new Vector2(goal.x+rad,goal.y+-centerHeight), 3*norm,"turm").setDna(CScommon.noPhotoBit, true).setDna(CScommon.vegetableBit, false);
		two = pushVegNode(new Vector2(goal.x+-rad,goal.y+-centerHeight),3*norm,"turm").setDna(CScommon.noPhotoBit, true);
		three = pushVegNode(new Vector2(goal.x+0,goal.y+height-centerHeight),3*norm,"turm").setDna(CScommon.noPhotoBit, true);

		Rules.TurmDefender.install(one,5*rad); 
		Rules.TurmDefender.install(two,5*rad); 
		Rules.TurmDefender.install(three,5*rad);
		
		//feeders are close
		feeder1 = pushVegNode(new Vector2(goal.x+(0.93f*rad),goal.y+(0.22f*rad)),1.9f*norm,"turm").setDna (CScommon.vegetableBit, false);
		feeder2 = pushVegNode(new Vector2(goal.x+(-1.2f*rad),goal.y+(-0.8f*rad)),3.1f*norm,"turm").setDna (CScommon.vegetableBit, false);
		feeder3 = pushVegNode(new Vector2(goal.x+(0.83f*rad),goal.y+(-0.38f*rad)),4.3f*norm,"turm").setDna (CScommon.vegetableBit, false);
		
		one.trust(goal); two.trust(goal); three.trust(goal); feeder1.trust(goal); feeder2.trust(goal); feeder3.trust(goal);
		
		//Weakness: Makes turm points heavier, but makes feeders easy to steal. Eat one and you've eaten the turm.
		feeder1.giveBurden (one);
		feeder2.giveBurden (two);
		feeder3.giveBurden (three);


		goal1 = pushVegNode(new Vector2(-Bub.worldRadius/2f, -Bub.worldRadius/4f),norm/4).setClan("turm1");
		bubbleServer.registerNPC(goal.id,"goal1");
		
		one1 = pushVegNode(new Vector2(goal1.x+rad,goal1.y+-centerHeight), 3*norm,"turm1").setDna(CScommon.noPhotoBit, true).setDna(CScommon.vegetableBit, false);
		two1 = pushVegNode(new Vector2(goal1.x+-rad,goal1.y+-centerHeight),3*norm,"turm1").setDna(CScommon.noPhotoBit, true);
		three1 = pushVegNode(new Vector2(goal1.x+0,goal1.y+height-centerHeight),3*norm,"turm1").setDna(CScommon.noPhotoBit, true);

		Rules.TurmDefender.install(one1,5*rad); 
		Rules.TurmDefender.install(two1,5*rad); 
		Rules.TurmDefender.install(three1,5*rad);
		
		//feeders are close
		feeder11 = pushVegNode(new Vector2(goal1.x+(0.93f*rad),goal1.y+(0.22f*rad)),1.9f*norm,"turm1").setDna (CScommon.vegetableBit, false);
		feeder21 = pushVegNode(new Vector2(goal1.x+(-1.2f*rad),goal1.y+(-0.8f*rad)),3.1f*norm,"turm1").setDna (CScommon.vegetableBit, false);
		feeder31 = pushVegNode(new Vector2(goal1.x+(0.83f*rad),goal1.y+(-0.38f*rad)),4.3f*norm,"turm1").setDna (CScommon.vegetableBit, false);
		
		one1.trust(goal1); two1.trust(goal1); three1.trust(goal1); feeder11.trust(goal1); feeder21.trust(goal1); feeder31.trust(goal1);

		//Weakness: Makes turm points heavier, but makes feeders easy to steal. Eat one and you've eaten the turm--though they're fairly large.
		//Pull them far away 
		feeder11.giveBurden (one1);
		feeder21.giveBurden (two1);
		feeder31.giveBurden (three1);



		//plant a bunch of munchies, but not within turm
		int startCount = Engine.nodes.Count;
		float turmRad = goal.distance (one);
		float turmRad1 = goal1.distance (one1);

		//beware, this becomes an infinite loop as turmRad approaches worldRadius
		while (Engine.nodes.Count - startCount < 100){
			plantRandomVeg(Random.Range(0.7f*norm, 1.4f*norm));
			if (Engine.nodes[Engine.nodes.Count - 1].distance(goal) < turmRad) Engine.nodes.RemoveAt (Engine.nodes.Count-1);
			if (Engine.nodes[Engine.nodes.Count - 1].distance(goal1) < turmRad1) Engine.nodes.RemoveAt (Engine.nodes.Count-1);
		}
		
		spawnRandomTeam (abnorm, 2, 2, 1, 1, "shirts", goal1);

		spawnRandomTeam (abnorm, 2, 2, 1, 2, "skins",  goal );

	}
	//****

//	public static void stdPlayers(float siz, List<Vector2> positions, int teamNumber){
//		Bub.Node head;
//		//mountable
//
//		head = spawnTricycle(new Vector2( Bub.worldRadius,0)    , 1*siz, false,
//			new Vector2( 10,0), 7, 0.75f*abnorm, false, "p1").setDna(CScommon.playerBit, true).setDna(CScommon.leftTeamBit,CScommon.rightTeamBit,0); 
//
//		head = spawnInchworm(new Vector2( Bub.worldRadius, -30)    , 1.2f*siz, false,
//			new Vector2( 10, 0), 1f*siz, false,"p2").setDna(CScommon.playerBit, true).setDna(CScommon.leftTeamBit,CScommon.rightTeamBit,0); 
//		
//		head = spawnTricycle(new Vector2( 0, Bub.worldRadius)   , 1*siz, false,
//			new Vector2( 0, 10), 7, 0.75f*siz, false, "p3").setDna(CScommon.playerBit, true).setDna(CScommon.leftTeamBit,CScommon.rightTeamBit,0); 
//
//		head = spawnInchworm(new Vector2( 30, Bub.worldRadius)   , 1.2f*siz, false,
//			new Vector2( 0, 10), 1f*siz, false,"p4").setDna(CScommon.playerBit, true).setDna(CScommon.leftTeamBit,CScommon.rightTeamBit,0);
//		
//		head = spawnTricycle(new Vector2( -Bub.worldRadius,0)   , 1*siz, false,
//			new Vector2( -10,0), 7, 0.75f*siz, false, "p5").setDna(CScommon.leftTeamBit,CScommon.rightTeamBit,1);
//		
//		head = spawnInchworm(new Vector2( -Bub.worldRadius,30)   , 1.2f*siz, false,
//			new Vector2( -10,0), 1f*siz, false,"p6").setDna(CScommon.playerBit, true).setDna(CScommon.leftTeamBit,CScommon.rightTeamBit,1); 
//
//		
//		head = spawnTricycle(new Vector2( 0, -Bub.worldRadius)   , 1*siz, false,
//			new Vector2( 0, -10), 7, 0.75f*siz, false, "p7").setDna(CScommon.playerBit, true).setDna(CScommon.leftTeamBit,CScommon.rightTeamBit,1); 
//
//		head = spawnInchworm(new Vector2( -30, -Bub.worldRadius)   , 1.2f*siz, false,
//			new Vector2( 0, -10), 1f*siz, false,"p8").setDna(CScommon.playerBit, true).setDna(CScommon.leftTeamBit,CScommon.rightTeamBit,1);
//		
//	}

	public static void testbedInit(){
		// Bub.Node pushVegNode(Vector2 position, float radius = 1.0f, string clan="")

		float small = norm/8;  float large = norm*8;

		pushVegNode(new Vector2(17.5f,0)*norm,small);
		pushVegNode(new Vector2(20,0)*norm,norm);
		pushVegNode(new Vector2(40,0)*norm,large);

		for (int i = Engine.nodes.Count - 3;i<Engine.nodes.Count;i++)Engine.nodes[i].oomph = 0;

		pushVegNode(new Vector2(17.5f,20)*norm,small);
		pushVegNode(new Vector2(20,20)*norm,norm);
		pushVegNode(new Vector2(40,20)*norm,large);

		for (int i = Engine.nodes.Count - 3;i<Engine.nodes.Count;i++)Engine.nodes[i].oomph = Engine.nodes[i].minBurden;

		pushVegNode(new Vector2(17.5f,40)*norm,small);
		pushVegNode(new Vector2(20,40)*norm,norm);
		pushVegNode(new Vector2(40,40)*norm,large);

		for (int i = Engine.nodes.Count - 3;i<Engine.nodes.Count;i++)Engine.nodes[i].oomph = Engine.nodes[i].maxOomph/2;

		pushVegNode(new Vector2(17.5f,60)*norm,small);
		pushVegNode(new Vector2(20,60)*norm,norm);
		pushVegNode(new Vector2(40,60)*norm,large);

		for (int i = Engine.nodes.Count - 3;i<Engine.nodes.Count;i++)Engine.nodes[i].oomph = Engine.nodes[i].maxOomph;

		/////animal

		pushVegNode(new Vector2(70+17.5f,0)*abnorm,small).setDna(CScommon.vegetableBit,false).setDna(CScommon.playerBit, true);
		pushVegNode(new Vector2(70+20,0)*abnorm,norm).setDna(CScommon.vegetableBit,false).setDna(CScommon.playerBit, true);
		pushVegNode(new Vector2(70+40,0)*abnorm,large).setDna(CScommon.vegetableBit,false).setDna(CScommon.playerBit, true);
		
		for (int i = Engine.nodes.Count - 3;i<Engine.nodes.Count;i++)Engine.nodes[i].oomph = 0;
		
		pushVegNode(new Vector2(70+17.5f,20)*abnorm,small).setDna(CScommon.vegetableBit,false).setDna(CScommon.playerBit, true);
		pushVegNode(new Vector2(70+20,20)*abnorm,norm).setDna(CScommon.vegetableBit,false).setDna(CScommon.playerBit, true);
		pushVegNode(new Vector2(70+40,20)*abnorm,large).setDna(CScommon.vegetableBit,false).setDna(CScommon.playerBit, true);
		
		for (int i = Engine.nodes.Count - 3;i<Engine.nodes.Count;i++)Engine.nodes[i].oomph = Engine.nodes[i].minBurden;
		
		pushVegNode(new Vector2(70+17.5f,40)*abnorm,small).setDna(CScommon.vegetableBit,false);
		pushVegNode(new Vector2(70+20,40)*abnorm,norm).setDna(CScommon.vegetableBit,false);
		pushVegNode(new Vector2(70+40,40)*abnorm,large).setDna(CScommon.vegetableBit,false);
		
		for (int i = Engine.nodes.Count - 3;i<Engine.nodes.Count;i++)Engine.nodes[i].oomph = Engine.nodes[i].maxOomph/2;
		
		pushVegNode(new Vector2(70+17.5f,60)*abnorm,small).setDna(CScommon.vegetableBit,false);
		pushVegNode(new Vector2(70+20,60)*abnorm,norm).setDna(CScommon.vegetableBit,false);
		pushVegNode(new Vector2(70+40,60)*abnorm,large).setDna(CScommon.vegetableBit,false);
		
	}


}
