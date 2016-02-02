//version 004

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Bots
{
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

	private static Bub.Node setUpPlayer(Bub.Node head, int teamNumber,  Bub.Node goal){
		head.setDna(CScommon.leftTeamBit, CScommon.rightTeamBit,teamNumber);
		head.setDna(CScommon.playerBit,true);
		Rules.Autopilot.install(head,goal); //does nothing if goal is null
		return head;
	}

	public static List<Bub.Node> spawnRandomTeam(float siz, int inchworms, int tricycles, int tapeworms, int teamNumber, string clan = "", Bub.Node goal = null){
		List<Bub.Node> players = new List<Bub.Node>();

		for (int i = 0; i< inchworms; i++){
			players.Add(setUpPlayer(spawnRandomInchworm(siz,false,false,clan), teamNumber, goal));
		}

		for (int i=0; i<tricycles; i++){
			players.Add(setUpPlayer(spawnRandomTricycle(siz,false,false,clan), teamNumber, goal));
		}

		for (int i=0; i<tapeworms; i++){
			players.Add(setUpPlayer(spawnRandomTapeworm(siz,false,false,5,clan), teamNumber, goal));
		}

		return players;
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
		
		int forward0Reverse1 = mount.getState ("forward0Reverse1");
		
		//so effect is immediate
		if (forward0Reverse1 == 0 && speed < 0) togglePushPull(mount);
		if (forward0Reverse1 == 1 && speed > 0) togglePushPull(mount);
		
		//so effect stands
		if (speed < 0) mount.setState ("forward0Reverse1", 1);
		else mount.setState ("forward0Reverse1",0);
		
		speed = Mathf.Abs (speed);
		List<Bub.Node> orgNodes = mount.trustGroup ();
		foreach (var node in orgNodes) node.enableInternalMuscles(speed);
	}

	public static void onTurn(int sourceId, int turn){
		if (turn >= -1 && turn <= 1) Engine.nodes[sourceId].setState("turn",turn);
	}

	//Mounting and dismounting.

	//returns whether or not there was anything to dismount
	public static bool dismount(int nodeId){
		if (nodeId < 0 || nodeId >= Engine.nodes.Count) return false;
		if (!Engine.nodes[nodeId].testDna(CScommon.playerPlayingBit)) return false;

		Engine.nodes[nodeId].setDna(CScommon.playerPlayingBit, false);
		return true;
	}

	//returns whether nodeId is mountable
	public static bool mountable(int nodeId){
		if (nodeId < 0 || nodeId >= Engine.nodes.Count) return false;
		if (!Engine.nodes[nodeId].testDna(CScommon.playerBit)) return false;
		if (Engine.nodes[nodeId].testDna(CScommon.playerPlayingBit)) return false; //already mounted
		return true;
	}

	//when a player mounts a node, they get the ability to command any pushPullServo that may be on the node,
	//and they get the ability to flee/attack via targeting
	public static bool mount(int nodeId){
		if (mountable(nodeId))  {
			Engine.nodes[nodeId].setDna(CScommon.playerPlayingBit, true);
			return true;
		}
		return false;
	}

	// initialization

	public static void initialize(int gameNumber){
		switch (gameNumber){
		case 1:
			bubbleServer.gameName = "snark";
			snarkInit();
			break;
		case 2:
			bubbleServer.gameName = "race";
			inchwormRaceInit();
			break;
		case 3:
			bubbleServer.gameName = "fussball";
			fussballInit();
			break;
		case 4:
			bubbleServer.gameName = "turm";
			turmInit();
			break;
		case 5:
			bubbleServer.gameName = "turm2";
			turmInit2();
			break;
		default: 
			bubbleServer.gameName = "sizeTest";
			testbedInit();
			break;
		}
	}

	//only specify goal if all PC's, as NPC's, should seek the same goal. Otherwise, add Autopilot individually
	private static void installAllPCRules(Bub.Node goal = null){
		for (int i = 0; i< Engine.nodes.Count; i++) if (Engine.nodes[i].testDna(CScommon.playerBit)){
				Rules.Autopilot.install(Engine.nodes[i],goal); //does nothing if goal == null
				Rules.HunterNPCRule.install(Engine.nodes[i]);
				Rules.HunterPCRule.install(Engine.nodes[i],0);
				Rules.HunterPCRule.install(Engine.nodes[i],1);
			}
	}

	public static void snarkInit(){
		Bub.Node head;
		Bub.Node bs,ls;


		//		head = pushVegNode(new Vector2(0,0), 7, "beast").setDna(CScommon.vegetableBit,false);
		//		Rules.installHunterNPCRule(head);
		//		
		//		head = spawnTricycle(new Vector2(0,-10),0.8f,false,
		//		                     new Vector2(8,-10),7, 0.6f, false,"prowler");
		//		Rules.installHunterNPCRule(head);

		head = spawnTapeworm(new Vector2(100,100),false, 7, bubbleServer.abnorm*0.8f, false ,"tapeworm");
		Rules.HunterNPCRule.install(head);  head.setDna(CScommon.playerBit, true);
		// don't track his score on snarks... bubbleServer.registerNPC(head.id,"tapeworm");

		head = spawnInchworm(new Vector2(-3,-3), bubbleServer.abnorm*1.2f, false, 
			new Vector2(7,7), bubbleServer.abnorm*1f, false,"snark"); 
		Rules.HunterNPCRule.install(head);
		head.setDna (CScommon.snarkBit,true);
		bubbleServer.registerNPC(head.id,"big snark");
		bs = head;

		head = spawnInchworm(new Vector2(0,10),bubbleServer.abnorm*0.6f,false,
			new Vector2(8,0), bubbleServer.abnorm*0.5f,false,"snark");
		Rules.HunterNPCRule.install(head);
		head.setDna (CScommon.snarkBit,true);
		bubbleServer.registerNPC(head.id,"lil snark");
		ls = head;


		for (int i=0; i<bubbleServer.popcorn; i++) plantRandomVeg(Random.Range(0.22f, 0.9f)*Random.Range(0.22f, 0.9f)*bubbleServer.norm); //random clans
		for (int i = 0; i<bubbleServer.popcorn/4; i++) {
			head = spawnRandomInchworm(bubbleServer.norm*Random.Range (0.48f,0.52f),true,true,"popcorn");
			Rules.Autopilot.install(head,ls);
		}
		//		for (int i = 0; i<60; i++) spawnRandomInchworm(Random.Range(0.5f, 2.0f)*normalBubRadius,true,true,"bots");

		stdPlayers(bubbleServer.abnorm);
		installAllPCRules(bs);

	}
	
	private static float offset(int i){ return bubbleServer.abnorm*i; }

	public static void inchwormRaceInit(){
		Bub.Node head, goal;

		goal = pushVegNode(new Vector2(-Bub.worldRadius,0),bubbleServer.norm*7, "goal").setDna(CScommon.vegetableBit,false); //goal left, won't eat anybody moving
		
		//evens against odds

		head = spawnTricycle(new Vector2( Bub.worldRadius,offset(5))    , bubbleServer.abnorm*1, false,
			new Vector2(2,0), 1.4f, bubbleServer.abnorm*0.75f, false, "Al").setDna(CScommon.playerBit, true); //nodes 1,2,3

		head = spawnInchworm(new Vector2( Bub.worldRadius,offset(8))    , bubbleServer.abnorm*1.2f, false,
			new Vector2(2,0), bubbleServer.abnorm*1f, false,"Beth").setDna(CScommon.playerBit, true); //nodes 4,5

		head = spawnTricycle(new Vector2( Bub.worldRadius,offset(11))    , bubbleServer.abnorm*1, false,
			new Vector2(2,0), 1.4f, bubbleServer.abnorm*0.75f, false, "Carl").setDna(CScommon.playerBit, true); //nodes 6,7,8

		head = spawnInchworm(new Vector2( Bub.worldRadius,offset(14))    , bubbleServer.abnorm*1.2f, false,
			new Vector2(2,0), bubbleServer.abnorm*1f, false,"Dee").setDna(CScommon.playerBit, true); //nodes 9,10

		
		head = spawnTricycle(new Vector2( Bub.worldRadius,-offset(5))   , bubbleServer.abnorm*1, false,
			new Vector2(2,0), 1.4f, bubbleServer.abnorm*0.75f, false, "Ed").setDna(CScommon.playerBit, true); //nodes 11.12.13

		head = spawnInchworm(new Vector2( Bub.worldRadius,-offset(8))   , bubbleServer.abnorm*1.2f, false,
			new Vector2(2,0), bubbleServer.abnorm*1f, false,"Fran").setDna(CScommon.playerBit, true); //nodes 14,15

		head = spawnTricycle(new Vector2( Bub.worldRadius,-offset(11))   , bubbleServer.abnorm*1, false,
			new Vector2(2,0), 1.4f, bubbleServer.abnorm*0.75f, false, "Greg").setDna(CScommon.playerBit, true); //nodes 16,17,18

		head = spawnInchworm(new Vector2( Bub.worldRadius,-offset(14))   , bubbleServer.abnorm*1.2f, false,
			new Vector2(2,0), bubbleServer.abnorm*1f, false,"Helen").setDna(CScommon.playerBit, true); //nodes 19,20
		

		//pushVegNode(new Vector2(Bub.worldRadius,0), bubbleServer.norm*7, "goal").setDna(CScommon.vegetableBit,false); //goal dright, won't eat anybody moving

		for (int i=0; i<bubbleServer.popcorn; i++) pushVegNode( randomRectPosition(0.17f),Random.Range(0.5f, 2.0f)*bubbleServer.norm); //random clans

		head = spawnInchworm(new Vector2(0,10),bubbleServer.abnorm*0.6f,false,
		                     new Vector2(8,0),bubbleServer.abnorm*0.5f,false,"pest");
		Rules.HunterNPCRule.install(head);
		bubbleServer.registerNPC(head.id,"inchworm pest");
		
		//head = spawnTricycle(new Vector2(0,-10),bubbleServer.abnorm*0.8f,false,
		//                     new Vector2(8,0),7, bubbleServer.abnorm*0.6f,false,"pest");
		//Rules.installHunterNPCRule(head);
		//bubbleServer.registerNPC(head.id,"tricycle pest");
		
		for (int i = 0; i<7; i++) spawnRandomInchworm(Random.Range(0.5f, 2.0f)*bubbleServer.norm,true,true,"bots");

		installAllPCRules(goal);

	}


	public static void fussballInit(){

		float rad = 12; // turn radius is half of turm side length
		float height = Mathf.Sqrt((rad*2)*(rad*2)-rad*rad); //height of equilateral triangle of sides = 2 rad
		float centerHeight = rad*rad/height; //height of center of that triangle
		float radius = height - centerHeight; //radius of circle circumscribed about three vertices
		Bub.Node crankA, crankB, crankC, one, two, three, center;

		//make triangle to swing the crank. 
		center = pushVegNode(new Vector2(0,0),0.7f*bubbleServer.norm,"crank");
		bubbleServer.registerNPC(center.id,"fussball");
		one = pushVegNode(new Vector2(-rad,-centerHeight),bubbleServer.norm,"crank");
		two = pushVegNode(new Vector2(rad,-centerHeight),bubbleServer.norm,"crank");
		three = pushVegNode(new Vector2(0,radius),bubbleServer.norm,"crank");

		//cranks are bigger, generate most power for the whole org
		crankA = pushVegNode(new Vector2(-rad,centerHeight),bubbleServer.norm*3,"crank");
		crankB = pushVegNode(new Vector2(rad,centerHeight),bubbleServer.norm*3,"crank");
		crankC = pushVegNode(new Vector2(0,-radius),bubbleServer.norm*3,"crank");
		
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

		for (int i=0; i<(bubbleServer.popcorn*3)/4; i++) plantRandomVeg(Random.Range(0.22f, 0.9f)*Random.Range(0.22f, 0.9f)*bubbleServer.norm); //random clans
		for (int i = 0; i<bubbleServer.popcorn/4; i++) {
			Rules.Autopilot.install(spawnRandomInchworm(bubbleServer.norm*Random.Range (0.48f,0.52f),true,true,"popcorn"),center);
		}
		//		for (int i = 0; i<60; i++) spawnRandomInchworm(Random.Range(0.5f, 2.0f)*normalBubRadius,true,true,"bots");
		stdPlayers(bubbleServer.abnorm);
		installAllPCRules(center);

	}
		

	//plants a turm of the given radius at the given position z
	public static Bub.Node plantTurm(Vector2 z, float rad){
		// turm radius is half of turm side length
		float height = Mathf.Sqrt((rad*2)*(rad*2)-rad*rad); //height of equilateral triangle of sides = 2 rad
		float centerHeight = rad*rad/height; //height of center of that triangle
		Bub.Node one, two, three, goal, feeder1, feeder2, feeder3;
		float norm = bubbleServer.norm;

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
		for (int i = 0; i<bubbleServer.popcorn/3; i++) {
			head = spawnRandomInchworm(bubbleServer.norm*Random.Range (0.7f,0.8f),true,true,"popcorn");
			head.enableInternalMuscles(50);
		}
		for (int i = 0; i<bubbleServer.popcorn/3; i++) {
			head = spawnRandomInchworm(bubbleServer.norm*Random.Range (0.7f,0.8f),true,true,"popcorn");
			head.enableInternalMuscles(50);
			Rules.Autopilot.install(head,goal1); 
		}
		for (int i = 0; i<bubbleServer.popcorn/3; i++) {
			head = spawnRandomInchworm(bubbleServer.norm*Random.Range (0.7f,0.8f),true,true,"popcorn");
			head.enableInternalMuscles(50);
			Rules.Autopilot.install(head,goal2);
		}

		spawnRandomTeam(bubbleServer.norm*1.4f, 3, 3, 1, 1, "shirts", goal2);
		spawnRandomTeam(bubbleServer.norm*1.4f, 3, 3, 1, 2, "skins" , goal1);

		installAllPCRules(); //separate autopilot goals already made in spawnRandomTeam
	}

	//***
	public static void turmInit2(){
		float rad = 30f; // turn radius is half of turm side length
		float height = Mathf.Sqrt((rad*2)*(rad*2)-rad*rad); //height of equilateral triangle of sides = 2 rad
		float centerHeight = rad*rad/height; //height of center of that triangle
		Bub.Node one, two, three, goal, feeder1, feeder2, feeder3;
		Bub.Node one1, two1, three1, goal1, feeder11, feeder21, feeder31;
		float norm = bubbleServer.norm;
		
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
		
		stdPlayers(bubbleServer.abnorm);
		installAllPCRules();
	}
	//****

	public static void stdPlayers(float siz){
		Bub.Node head;
		//mountable
		head = spawnTricycle(new Vector2( Bub.worldRadius,0)    , 1*siz, false,
			new Vector2( 10,0), 7, 0.75f*bubbleServer.abnorm, false, "p1").setDna(CScommon.playerBit, true).setDna(CScommon.leftTeamBit,CScommon.rightTeamBit,0); 

		head = spawnInchworm(new Vector2( Bub.worldRadius, -30)    , 1.2f*siz, false,
			new Vector2( 10, 0), 1f*siz, false,"p2").setDna(CScommon.playerBit, true).setDna(CScommon.leftTeamBit,CScommon.rightTeamBit,0); 
		
		head = spawnTricycle(new Vector2( 0, Bub.worldRadius)   , 1*siz, false,
			new Vector2( 0, 10), 7, 0.75f*siz, false, "p3").setDna(CScommon.playerBit, true).setDna(CScommon.leftTeamBit,CScommon.rightTeamBit,0); 

		head = spawnInchworm(new Vector2( 30, Bub.worldRadius)   , 1.2f*siz, false,
			new Vector2( 0, 10), 1f*siz, false,"p4").setDna(CScommon.playerBit, true).setDna(CScommon.leftTeamBit,CScommon.rightTeamBit,0);
		
		head = spawnTricycle(new Vector2( -Bub.worldRadius,0)   , 1*siz, false,
			new Vector2( -10,0), 7, 0.75f*siz, false, "p5").setDna(CScommon.leftTeamBit,CScommon.rightTeamBit,1);
		
		head = spawnInchworm(new Vector2( -Bub.worldRadius,30)   , 1.2f*siz, false,
			new Vector2( -10,0), 1f*siz, false,"p6").setDna(CScommon.playerBit, true).setDna(CScommon.leftTeamBit,CScommon.rightTeamBit,1); 

		
		head = spawnTricycle(new Vector2( 0, -Bub.worldRadius)   , 1*siz, false,
			new Vector2( 0, -10), 7, 0.75f*siz, false, "p7").setDna(CScommon.playerBit, true).setDna(CScommon.leftTeamBit,CScommon.rightTeamBit,1); 

		head = spawnInchworm(new Vector2( -30, -Bub.worldRadius)   , 1.2f*siz, false,
			new Vector2( 0, -10), 1f*siz, false,"p8").setDna(CScommon.playerBit, true).setDna(CScommon.leftTeamBit,CScommon.rightTeamBit,1);
		
	}

	public static void testbedInit(){
		// Bub.Node pushVegNode(Vector2 position, float radius = 1.0f, string clan="")

		float small = bubbleServer.norm/8;  float norm = bubbleServer.norm; float large = bubbleServer.norm*8;

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
		float abnorm = bubbleServer.abnorm;

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
