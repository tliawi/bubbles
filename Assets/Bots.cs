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
		Rules.installPush1Pull2Servo(head,tailList);
		Rules.installNearFarPush1Pull2Cmdr(head,tailList, Random.Range (2f,4f),Random.Range (10f,20f));
		return head;
	}

	public static void spawnRandomInchworm(float approximateRadius, bool headVeg, bool tailVeg, string clan=""){
		Vector2 headPosition = Bub.worldRadius * Random.insideUnitCircle;
		float randomAngle = Random.Range(-Mathf.PI, Mathf.PI);
		Vector2 tailDelta = 8*(new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle))) ;
		float headRadius = approximateRadius;
		float tailRadius = approximateRadius*1.4f;
		spawnInchworm(headPosition,headRadius,headVeg,tailDelta,tailRadius,tailVeg, clan);
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
		Rules.installPush1Pull2Servo(head,tailList);
		Rules.installTurnServo(head,tailL,tailR);

		// make equilateral triangle at near end
		Rules.installNearFarPush1Pull2Cmdr(head,tailList, 0.75f*widthBetweenTails ,1.4f*widthBetweenTails);

		return head;
	}


	public static float wanderAngle(float wander, float angle){
		wander = Mathf.Abs (wander);
		return angle + Random.Range (-wander, wander);
	}

	//it's a good idea to make headRadius tiny compared with tailRadius, unless you install a rule on head that lets head shift its burden when being pushed forward...
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

			//Rules.installSegmentPushPullServo(priorTail,tail, n);
			//Rules.installNearFarPush1Pull2Cmdr(priorTail,Rules.nodeList(tail), 3,10);//6.5 is avg of 3 and 10...
			Rules.installSegmentPulltokenServo(priorTail,tail);
		}

		//make final tail tiny, so is light to push, since that weight won't be shifted off it
		tail.setRadius(radius/5);

		return head;
	}


	////////Event handlers called from bubbleServer

	public static void onTarget(int sourceId, int targetId, CScommon.LinkType linkType){
		if (sourceId >= 0 && sourceId < Engine.nodes.Count && targetId >= 0 && targetId < Engine.nodes.Count
		    && linkType != CScommon.LinkType.bone) {

			int targetIdp1 = targetId + 1;
			if (linkType == CScommon.LinkType.pusher) targetIdp1 = -targetIdp1;
			Engine.nodes[sourceId].setState("targetIdp1", targetIdp1); // push-, pull+, (targetId +1). 0 means stop fighting

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
	public static void onPush1Pull2(int sourceId, int push1Pull2){
	
		switch (push1Pull2) {
		
			case 0:  
				Engine.nodes[sourceId].setState ("nearFarSwitch01", 1); //enable automatic nearFar cmdr
				break;
			case 1:
			case 2:
				Engine.nodes[sourceId].setState ("nearFarSwitch01", 0); //disable automatic cmdr
				Engine.nodes[sourceId].setState ("push1Pull2",push1Pull2); 
				break;
			case 3: 
				Engine.nodes[sourceId].setState ("nearFarSwitch01", 0); //disable automatic cmdr
				togglePushPull(Engine.nodes[sourceId]);
				break;
			}
	}

	//reversal effect: push pull servo shifts burden to determine which end moves more during push bzw pull
	//0 means forward, 1 means reverse. Anything else means toggle.
	public static void onForward0Reverse1(int sourceId, int forward0Reverse1){
		int pre = Engine.nodes[sourceId].getState("forward0Reverse1");

		if (forward0Reverse1 == 0 || forward0Reverse1 == 1)	Engine.nodes[sourceId].setState("forward0Reverse1",forward0Reverse1);
		else Engine.nodes[sourceId].setState("forward0Reverse1",Engine.nodes[sourceId].checkState("forward0Reverse1",1)?0:1);
		
		//compare pre to post
		if (pre != Engine.nodes[sourceId].getState("forward0Reverse1")) togglePushPull(Engine.nodes[sourceId]); //so has immediate effect

		//bubbleServer.debugDisplay ("fr "+forward0Reverse1+" "+Engine.nodes[sourceId].getState ("forward0Reverse1"));
	}

	public static void onTurn(int sourceId, int turn){
		if (turn >= -1 && turn <= 1) Engine.nodes[sourceId].setState("turn",turn);
	}

	//Mounting and dismounting.


	public static void dismount(int nodeId){
		if (nodeId < 0 || nodeId >= Engine.nodes.Count) return;

		Engine.nodes[nodeId].setDna (CScommon.playerBit, false);
		Engine.nodes[nodeId].setDna(CScommon.playerPlayingBit, false);

		Engine.nodes[nodeId].rules.RemoveAt (Engine.nodes[nodeId].rules.Count - 1); //remove rule added when you mounted
	}

	//when a player mounts a node, they get the ability to command any pushPullServo that may be on the node,
	//and they get the ability to flee/attack via targeting
	
	public static void mount(int nodeId){
		if (nodeId < 0 || nodeId >= Engine.nodes.Count) return;

		Engine.nodes[nodeId].setDna(CScommon.playerBit, true);
		Engine.nodes[nodeId].setDna(CScommon.playerPlayingBit, true);

		Rules.installHunterPCRule(Engine.nodes[nodeId]);
	}

	// initialization

	public static void initialize(int gameNumber, float norm, float abnorm){
		switch (gameNumber){
		case 1:
			bubbleServer.gameName = "snark";
			snarkInit(norm, abnorm);
			break;
		case 2:
			bubbleServer.gameName = "race";
			inchwormRaceInit(norm, abnorm);
			break;
		case 3:
			bubbleServer.gameName = "fussball";
			fussballInit(norm, abnorm);
			break;
		case 4:
			bubbleServer.gameName = "turm";
			turmInit(norm,abnorm);
			break;
		default: 
			bubbleServer.gameName = "sizeTest";
			testbedInit(norm, abnorm);
			break;
		}
	}

	//called from bubbleServer whenever it enters second game phase
	//disables unregistered players
	public static void startRace(){
//		foreach (Bub.Node node in Engine.nodes) {
//
//			if (!CScommon.testBit(node.dna,CScommon.vegetableBit)
//			    && !bubbleServer.registered (node.id)
//			    && node.getState ("nearFarSwitch01") != int.MinValue ){ //i.e. it has a nearFarSwitch at all
//
//					node.setState ("nearFarSwitch01",0);
//					node.setState ("push1Pull2", 0); //anything but 1 or 2 disables push1Pull2
//			}
//		}
	}
	

	public static void inchwormRaceInit(float norm, float abnorm){
		
		pushVegNode(new Vector2(-Bub.worldRadius,0), norm*7, "goal").setDna(CScommon.vegetableBit,false); //goal left, won't eat anybody moving
		
		//evens against odds
		Bub.Node head;
		
		head = spawnTricycle(new Vector2( Bub.worldRadius,25)    , abnorm*1, false,
		                     new Vector2(10,0), 7, abnorm*0.75f, false, "Al"); //nodes 1,2,3
		//done when mount Rules.installHunterPCRule(head);
		
		head = spawnInchworm(new Vector2( Bub.worldRadius,35)    , abnorm*1.2f, false,
		                     new Vector2(10,0) , abnorm*1f, false,"Beth"); //nodes 4,5
		//done when mount Rules.installHunterPCRule(head);
		
		head = spawnTricycle(new Vector2( Bub.worldRadius,45)    , abnorm*1, false,
		                     new Vector2(10,0), 7, abnorm*0.75f, false, "Carl"); //nodes 6,7,8
		//done when mount Rules.installHunterPCRule(head);
		
		head = spawnInchworm(new Vector2( Bub.worldRadius,55)    , abnorm*1.2f, false,
		                     new Vector2(10,0) , abnorm*1f, false,"Dee"); //nodes 9,10
		//done when mount Rules.installHunterPCRule(head);
		
		
		head = spawnTricycle(new Vector2( Bub.worldRadius,-25)   , abnorm*1, false,
		                     new Vector2( 10,0), 7, abnorm*0.75f, false, "Ed"); //nodes 11.12.13
		//done when mount Rules.installHunterPCRule(head);
		
		head = spawnInchworm(new Vector2( Bub.worldRadius,-35)   , abnorm*1.2f, false,
		                     new Vector2( 10,0), abnorm*1f, false,"Fran"); //nodes 14,15
		//done when mount Rules.installHunterPCRule(head);
		
		head = spawnTricycle(new Vector2( Bub.worldRadius,-45)   , abnorm*1, false,
		                     new Vector2( 10,0), 7, abnorm*0.75f, false, "Greg"); //nodes 16,17,18
		//done when mount Rules.installHunterPCRule(head);
		
		head = spawnInchworm(new Vector2( Bub.worldRadius,-55)   , abnorm*1.2f, false,
		                     new Vector2( 10,0), abnorm*1f, false,"Helen"); //nodes 19,20
		//done when mount Rules.installHunterPCRule(head);


		pushVegNode(new Vector2(Bub.worldRadius,0), norm*7, "goal").setDna(CScommon.vegetableBit,false); //goal left, won't eat anybody moving


		for (int i=0; i<30; i++) pushVegNode( randomRectPosition(0.17f),Random.Range(0.5f, 2.0f)*norm); //random clans
		
		
		head = spawnInchworm(new Vector2(0,10),abnorm*0.6f,false,
		                     new Vector2(8,0),abnorm*0.5f,false,"pest");
		Rules.installHunterNPCRule(head);
		bubbleServer.registerNPC(head.id,"inchworm pest");
		
		//head = spawnTricycle(new Vector2(0,-10),abnorm*0.8f,false,
		//                     new Vector2(8,0),7, abnorm*0.6f,false,"pest");
		//Rules.installHunterNPCRule(head);
		//bubbleServer.registerNPC(head.id,"tricycle pest");
		
		for (int i = 0; i<7; i++) spawnRandomInchworm(Random.Range(0.5f, 2.0f)*norm,true,true,"bots");

	}

	public static void snarkInit(float norm, float abnorm){
		Bub.Node head;


//		head = pushVegNode(new Vector2(0,0), 7, "beast").setDna(CScommon.vegetableBit,false);
//		Rules.installHunterNPCRule(head);
//		
//		head = spawnTricycle(new Vector2(0,-10),0.8f,false,
//		                     new Vector2(8,-10),7, 0.6f, false,"prowler");
//		Rules.installHunterNPCRule(head);

		head = spawnTapeworm(new Vector2(100,100),false, 7, abnorm*0.8f, false ,"tapeworm");
		Rules.installHunterNPCRule(head);
		// don't track his score on snarks... bubbleServer.registerNPC(head.id,"tapeworm");

		head = spawnInchworm(new Vector2(-3,-3), abnorm*1.2f, false, 
		                     new Vector2(7,7), abnorm*1f, false,"snark"); 
		Rules.installHunterNPCRule(head);
		head.setDna (CScommon.snarkBit,true);
		bubbleServer.registerNPC(head.id,"big snark");
		
		head = spawnInchworm(new Vector2(0,10),abnorm*0.6f,false,
		                     new Vector2(8,0), abnorm*0.5f,false,"snark");
		Rules.installHunterNPCRule(head);
		head.setDna (CScommon.snarkBit,true);
		bubbleServer.registerNPC(head.id,"lil snark");


		
		head = spawnTricycle(new Vector2( Bub.worldRadius,0), 1*abnorm, false,
		                     new Vector2( 10,0), 7, 0.75f*abnorm, false, "p1"); 
		//done when mount Rules.installHunterPCRule(head);
		
		head = spawnInchworm(new Vector2( Bub.worldRadius, -30), 1.2f*abnorm, false,
		                     new Vector2( 10,0), 1f*abnorm, false,"p2"); 
		//done when mount Rules.installHunterPCRule(head);


		head = spawnTricycle(new Vector2( 0, Bub.worldRadius), 1*abnorm, false,
		                     new Vector2( 0, 10), 7, 0.75f*abnorm, false, "p3"); 
		//done when mount Rules.installHunterPCRule(head);
		
		head = spawnInchworm(new Vector2( 30, Bub.worldRadius), 1.2f*abnorm, false,
		                     new Vector2( 0, 10), 1f*abnorm, false,"p4");
		//done when mount Rules.installHunterPCRule(head);


		head = spawnTricycle(new Vector2( -Bub.worldRadius,0)   , 1*abnorm, false,
		                     new Vector2( -10,0), 7, 0.75f*abnorm, false, "p5"); 
		//done when mount Rules.installHunterPCRule(head);
		
		head = spawnInchworm(new Vector2( -Bub.worldRadius,30)   , 1.2f*abnorm, false,
		                     new Vector2( -10,0), 1f*abnorm, false,"p6"); 
		//done when mount Rules.installHunterPCRule(head);


		head = spawnTricycle(new Vector2( 0, -Bub.worldRadius)   , 1*abnorm, false,
		                     new Vector2( 0, -10), 7, 0.75f*abnorm, false, "p7"); 
		//done when mount Rules.installHunterPCRule(head);
		
		head = spawnInchworm(new Vector2( -30, -Bub.worldRadius)   , 1.2f*abnorm, false,
		                     new Vector2( 0, -10), 1f*abnorm, false,"p8");
		//done when mount Rules.installHunterPCRule(head);
		

		for (int i=0; i<110; i++) plantRandomVeg(Random.Range(0.22f, 0.9f)*Random.Range(0.22f, 0.9f)*norm); //random clans

//		for (int i = 0; i<60; i++) spawnRandomInchworm(Random.Range(0.5f, 2.0f)*normalBubRadius,true,true,"bots");

	}

	public static void fussballInit(float norm, float abnorm){
		Bub.Node head;

		head = pushVegNode(new Vector2(0,0),norm/4);
		bubbleServer.registerNPC(head.id,"fussball");
		

		head = spawnTricycle(new Vector2( Bub.worldRadius,0)    , 1*abnorm, false,
		                     new Vector2( 10,0), 7, 0.75f*abnorm, false, "p1"); 
		//done when mount Rules.installHunterPCRule(head);
		
		head = spawnInchworm(new Vector2( Bub.worldRadius, -30)    , 1.2f*abnorm, false,
		                     new Vector2( 10, 0), 1f*abnorm, false,"p2"); 
		//done when mount Rules.installHunterPCRule(head);
		
		
		head = spawnTricycle(new Vector2( 0, Bub.worldRadius)   , 1*abnorm, false,
		                     new Vector2( 0, 10), 7, 0.75f*abnorm, false, "p3"); 
		//done when mount Rules.installHunterPCRule(head);
		
		head = spawnInchworm(new Vector2( 30, Bub.worldRadius)   , 1.2f*abnorm, false,
		                     new Vector2( 0, 10), 1f*abnorm, false,"p4");
		//done when mount Rules.installHunterPCRule(head);
		
		
		head = spawnTricycle(new Vector2( -Bub.worldRadius,0)   , 1*abnorm, false,
		                     new Vector2( -10,0), 7, 0.75f*abnorm, false, "p5"); 
		//done when mount Rules.installHunterPCRule(head);
		
		head = spawnInchworm(new Vector2( -Bub.worldRadius,30)   , 1.2f*abnorm, false,
		                     new Vector2( -10,0), 1f*abnorm, false,"p6"); 
		//done when mount Rules.installHunterPCRule(head);
		
		
		head = spawnTricycle(new Vector2( 0, -Bub.worldRadius)   , 1*abnorm, false,
		                     new Vector2( 0, -10), 7, 0.75f*abnorm, false, "p7"); 
		//done when mount Rules.installHunterPCRule(head);
		
		head = spawnInchworm(new Vector2( -30, -Bub.worldRadius)   , 1.2f*abnorm, false,
		                     new Vector2( 0, -10), 1f*abnorm, false,"p8");
		//done when mount Rules.installHunterPCRule(head);
		
		
		for (int i=0; i<110; i++) plantRandomVeg(Random.Range(0.22f, 0.9f)*Random.Range(0.22f, 0.9f)*norm); //random clans
		
		//		for (int i = 0; i<60; i++) spawnRandomInchworm(Random.Range(0.5f, 2.0f)*normalBubRadius,true,true,"bots");

	}

	public static void turmInit(float norm,float abnorm){
		float rad = 12; // turn radius is half of turm side length
		float height = Mathf.Sqrt((rad*2)*(rad*2)-rad*rad); //height of equilateral triangle of sides = 2 rad
		float centerHeight = rad*rad/height; //height of center of that triangle
		Bub.Node one, two, three, goal, feeder1, feeder2, feeder3;

		goal = pushVegNode(new Vector2(0,0),norm/4).setClan("turm");
		bubbleServer.registerNPC(goal.id,"goal");
		
		one = pushVegNode(new Vector2(rad,-centerHeight),3*norm,"turm").setDna(CScommon.noPhotoBit, true).setDna(CScommon.vegetableBit, false);
		two = pushVegNode(new Vector2(-rad,-centerHeight),3*norm,"turm").setDna(CScommon.noPhotoBit, true);
		three = pushVegNode(new Vector2(0,height-centerHeight),3*norm,"turm").setDna(CScommon.noPhotoBit, true);

		Rules.installTurmDefender(one,5*rad); 
		Rules.installTurmDefender(two,5*rad); 
		Rules.installTurmDefender(three,5*rad);
		
		//feeders are close
		feeder1 = pushVegNode(new Vector2(0.93f*rad,0.22f*rad),1.9f*norm,"turm").setDna (CScommon.vegetableBit, false);
		feeder2 = pushVegNode(new Vector2(-1.2f*rad,-0.8f*rad),3.1f*norm,"turm").setDna (CScommon.vegetableBit, false);
		feeder3 = pushVegNode(new Vector2(0.83f*rad,-0.38f*rad),4.3f*norm,"turm").setDna (CScommon.vegetableBit, false);

		one.trust(goal); two.trust(goal); three.trust(goal); feeder1.trust(goal); feeder2.trust(goal); feeder3.trust(goal);

		//Weakness: Makes turm points heavier, but makes feeders easy to steal. Eat one and you've eaten the turm.
		feeder1.giveBurden (one);
		feeder2.giveBurden (two);
		feeder3.giveBurden (three);

		//plant a bunch of munchies, but not within turm
		int startCount = Engine.nodes.Count;
		float turmRad = goal.distance (one);
		//beware, this becomes an infinite loop as turmRad approaches worldRadius
		while (Engine.nodes.Count - startCount < 100){
			plantRandomVeg(Random.Range(0.7f*norm, 1.4f*norm));
			if (Engine.nodes[Engine.nodes.Count - 1].distance(goal) < turmRad) Engine.nodes.RemoveAt (Engine.nodes.Count-1);
		}

		//mountable
		spawnTricycle(new Vector2( Bub.worldRadius,0)    , 1*abnorm, false,
		              new Vector2( 10,0), 7, 0.75f*abnorm, false, "p1"); 
		
		spawnInchworm(new Vector2( Bub.worldRadius, -30)    , 1.2f*abnorm, false,
		              new Vector2( 10, 0), 1f*abnorm, false,"p2"); 
		
		
		spawnTricycle(new Vector2( 0, Bub.worldRadius)   , 1*abnorm, false,
		              new Vector2( 0, 10), 7, 0.75f*abnorm, false, "p3"); 
		
		spawnInchworm(new Vector2( 30, Bub.worldRadius)   , 1.2f*abnorm, false,
		              new Vector2( 0, 10), 1f*abnorm, false,"p4");
		
		
		spawnTricycle(new Vector2( -Bub.worldRadius,0)   , 1*abnorm, false,
		              new Vector2( -10,0), 7, 0.75f*abnorm, false, "p5");
		
		spawnInchworm(new Vector2( -Bub.worldRadius,30)   , 1.2f*abnorm, false,
		              new Vector2( -10,0), 1f*abnorm, false,"p6"); 
		
		
		spawnTricycle(new Vector2( 0, -Bub.worldRadius)   , 1*abnorm, false,
		              new Vector2( 0, -10), 7, 0.75f*abnorm, false, "p7"); 
		
		spawnInchworm(new Vector2( -30, -Bub.worldRadius)   , 1.2f*abnorm, false,
		              new Vector2( 0, -10), 1f*abnorm, false,"p8");
		

	}

	public static void testbedInit(float norm,float abnorm){
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

		pushVegNode(new Vector2(70+17.5f,0)*abnorm,small).setDna(CScommon.vegetableBit,false);
		pushVegNode(new Vector2(70+20,0)*abnorm,norm).setDna(CScommon.vegetableBit,false);
		pushVegNode(new Vector2(70+40,0)*abnorm,large).setDna(CScommon.vegetableBit,false);
		
		for (int i = Engine.nodes.Count - 3;i<Engine.nodes.Count;i++)Engine.nodes[i].oomph = 0;
		
		pushVegNode(new Vector2(70+17.5f,20)*abnorm,small).setDna(CScommon.vegetableBit,false);
		pushVegNode(new Vector2(70+20,20)*abnorm,norm).setDna(CScommon.vegetableBit,false);
		pushVegNode(new Vector2(70+40,20)*abnorm,large).setDna(CScommon.vegetableBit,false);
		
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
