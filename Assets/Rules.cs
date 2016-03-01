// copyright 2015-2016 John Fairfield

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bubbles{
	public class Rules {

		//given an angle in radians, returns the equivalent between -PI and +PI
		public static float stdAngle( float angl)
		{	while (angl < -Mathf.PI) angl += 2*Mathf.PI;
			while (angl > Mathf.PI) angl -= 2*Mathf.PI;
			return angl;
		}
		
		// Angle abc. Given three points a,b,c
		// returns the angle at the middle one, b.
		// Is positive if angle from a to b to c is ccwise(standard math positive angle direction, think of a lying on the +x axis, b at origin), 
		// negative if cwise.
		public static float signedAngle( Node a, Node b, Node c)
		{	float angle1 = Mathf.Atan2(a.y-b.y, a.x-b.x);
			float angle2 = Mathf.Atan2(c.y-b.y, c.x-b.x);
			return stdAngle(angle2 - angle1);
		}

		//for use with b from orgCOB
		public static float signedAngle(Node a, Vector2 b, Node c)
		{	float angle1 = Mathf.Atan2(a.y-b.y, a.x-b.x);
			float angle2 = Mathf.Atan2(c.y-b.y, c.x-b.x);
			return stdAngle(angle2 - angle1);
		}

		//for use with c from orgCOB
		public static float signedAngle(Node a, Node b, Vector2 c)
		{	float angle1 = Mathf.Atan2(a.y-b.y, a.x-b.x);
			float angle2 = Mathf.Atan2(c.y-b.y, c.x-b.x);
			return stdAngle(angle2 - angle1);
		}

		//overloaded helper fcn
		public static List<Node> nodeList(Node nod0){
			List<Node> lst =  new List<Node>();
			lst.Add (nod0);
			return lst;
		}
		
		public static List<Node> nodeList(Node nod0, Node nod1){
			List<Node> lst =  new List<Node>();
			lst.Add (nod0);
			lst.Add (nod1);
			return lst;
		}

		public static List<Node> nodeList(Node nod0, Node nod1, Node nod2){
			List<Node> lst =  new List<Node>();
			lst.Add (nod0);
			lst.Add (nod1);
			lst.Add (nod2);
			return lst;
		}

		public struct SteeringStruct{
			public Node target;
			public float sideEffect; //positive if pull will pull you to the left, negative if pull will pull you to the right, and proportional to the steering effect
		}

		public abstract class Rule {

			public Node source {get; protected set;}
			private List<Muscle> _muscles;

			public Muscle muscles( int i) { return _muscles[i]; }
			
			public int musclesCount { get{return _muscles.Count;} }

			protected Muscle addMuscle(Node target){ 
				Muscle muscle = new Muscle(source,target);
				_muscles.Add (muscle);
				return muscle;
			}

			public void disableMuscles(){
				for (int i = 0; i< _muscles.Count; i++) _muscles[i].disable ();
			}

			public void enableInternalMuscles(int percent)
				{	for (int i = 0; i< _muscles.Count; i++) if (source.org == _muscles[i].target.org) _muscles[i].enable (percent); //enable does nothing to a cut muscle
			}

			public void enableMuscles(int percent){
					for (int i = 0; i< _muscles.Count; i++) _muscles[i].enable (percent);//enable does nothing to a cut muscle
			}

			public void reEnableMuscles(){
					for (int i = 0; i< _muscles.Count; i++) _muscles[i].reEnable ();//reEnable does nothing to a cut muscle
			}

			public int cutExternalMuscles(){
				int sum = 0;
				for (int i = 0; i < _muscles.Count; i++)
					if (_muscles [i].external) {
						sum += 1;
						_muscles [i].cut ();
					}
				return sum;
			}

			public void cutAllMuscles(){
				for (int i = 0; i < _muscles.Count; i++)
					_muscles [i].cut ();
			}

			public void cutMusclesTargetingOrg(Org org){
				for (int i=0; i< _muscles.Count; i++) if (_muscles[i].target.org == org) _muscles[i].cut();
			}
			
			abstract public void accion(); //rule condition, state changes, muscle property changes

			public float muscleActionDemand(){ //rule muscle actions
				float demand = 0;
				for (int i = 0; i<_muscles.Count; i++) demand += _muscles[i].actionDemand();
				return demand;
			}

			public void muscleAction(float fraction){ //rule muscle actions
				for (int i = 0; i<_muscles.Count; i++)_muscles[i].action(fraction);
			}

			protected Rule(Node source0) {
				source = source0;
				_muscles = new List<Muscle>();
			}
		}
		


		//disabled by source.setState("turn",0)
		public class TurnServo: Rule{

			public static void install(Node source, Node firstTarget, Node lastTarget){
				if (source == null || firstTarget == null || lastTarget == null) return;
				
				source.rules.Add (new TurnServo(source, firstTarget, lastTarget));
			}

			int countDown;
			List<Node> sourceList;
			List<Node> targetList;

			private TurnServo(Node source0, Node firstTarget, Node lastTarget):base(source0){

				//ASSUMPTION: order of muscles sweeps from L to R,
				addMuscle(firstTarget);
				addMuscle(lastTarget);
				disableMuscles();

				sourceList = nodeList(source);
				targetList = nodeList(firstTarget, lastTarget);

				countDown = 0;
				source.setState ("turn",0);
			}

			//only call on internal muscles, which are never cut (have identical source and target)
			private void flipHandedness(){
				Node x = muscles(0).target;
				muscles(0).reTarget(muscles(1).target);
				muscles(1).reTarget(x);
			}

			public override void accion() {

				if (signedAngle(muscles(1).target,muscles(0).target,source)<0) flipHandedness();

				int turn = source.getState ("turn"); //-1, 0, +1, 12345 (during a turn)

				if (turn == 0 ) { disableMuscles(); countDown = 0; return;}//end turn early....
				
				if (turn == 1 || turn == -1) { //starting a turn
					source.setState ("turn", 12345); //signal that I am doing a turn

					Org.shiftBurden (0,sourceList,targetList);
					
					//make muscles work at cross purposes, for elliptical movement (movement of head in an elliptical orbit about tails)
					if (turn < 0) { muscles(0).makePuller(); muscles(1).makePusher();} //L 
					else {muscles(0).makePusher(); muscles(1).makePuller ();} //R

					reEnableMuscles();

					countDown = 6;
				}
				
				if (countDown > 0) countDown -= 1;

				//manageWeightsToEqualizeStrength(false,firstMuscle,lastMuscle);
				
				if (countDown == 0) {
					disableMuscles();
					source.setState ("turn", 0); //signal that I am finished turning
				}

			}
		}


		public class Push1Pull2Servo: Rule {
			
			public static void install(Node source, List<Node> targetList){
				if (source == null || targetList.Contains (null)) return;
				
				source.rules.Add (new Push1Pull2Servo(source, targetList));
			}
		
			int priorPushPull;
			List<Node> sourceList, targetList;

			private Push1Pull2Servo( Node source0, List<Node> targetList0):base(source0){

				targetList = targetList0;

				priorPushPull = 0;

				for (int i=0; i< targetList.Count; i++) addMuscle(targetList[i]);

				sourceList = nodeList(source);

				source.setState ("push1Pull2",2);
				source.setState ("forward0Reverse1",0);
				source.setState ("turn", 0); //so that getState("turn") is never undefined
			}

			public override void accion() {

				//suppress during turns, but ignore turn if an inchworm
				if (musclesCount < 2 || source.getState("turn") == 0) { 
						
					int push1Pull2;
					int forwardReverse;

					push1Pull2 = source.getState("push1Pull2"); //used by manual onPush1Pull2 or automatic nearFarPush1Pull2Cmdr to control this rule
					
					if (push1Pull2 == 0) { 
						priorPushPull = 0;
						disableMuscles();
						return; 
					}

					if (push1Pull2 != priorPushPull) {
						reEnableMuscles();
						priorPushPull = push1Pull2;
						forwardReverse = source.getState ("forward0Reverse1");

						if (push1Pull2 == 1 ){
							Org.shiftBurden(forwardReverse,sourceList,targetList);
							for (int i=0; i< musclesCount; i++) muscles(i).makePusher();
							
						}else if (push1Pull2 == 2){
							Org.shiftBurden(1-forwardReverse,sourceList,targetList);
							for (int i=0; i< musclesCount; i++) muscles(i).makePuller();
						}
					}

					//Pushing naturally tends to equalize isoceles triangle--shorter link is more efficient, so pushes more.
					//Pulling has the opposite effect, so need to counteract
					//NOT NEEDED WITH INTERNAL BONES
//					if (musclesCount >= 2 && push1Pull2 == 2 ) {
//						if (muscles(0).efficiency() > muscles(musclesCount-1).efficiency()){ 
//							muscles(0).disable(); muscles(musclesCount-1).reEnable ();
//						} else {
//							muscles(0).reEnable(); muscles(musclesCount-1).disable ();
//						}
//					}
				} else { disableMuscles(); priorPushPull = 0; } //so will reinitialize pushPull after turn is finished
			}

		}



		
		//Controls push1Pull2Servo via push1Pull2 state.
		//Integer versions of Near and Far could be kept in state if we want to make this rule modifyable by other rules or actions.
		public class NearFarPush1Pull2Cmdr:Rule {

			//A Servo is a rule with muscles.
			//A Cmdr is a rule with musclesCount == 0 that takes the place of a user via manipulating state only, thereby controlling servo rules
			//near and far are in terms of link gap, i.e. distance - sum of radii of source and target
			public static void install(Node source0, List<Node> targets0, float near0, float far0){
				if (source0 == null || targets0.Contains (null)) return;
				source0.rules.Add (new NearFarPush1Pull2Cmdr(source0, targets0, near0, far0));
			}

			float near, far, priorAvgLn;
			List<Node> targets;

			private NearFarPush1Pull2Cmdr(Node source0, List<Node> targets0, float near0, float far0):base(source0) {
			
				targets = targets0;
				near = near0;
				far = far0;
				priorAvgLn = -1000;
				source.setState ("nearFarSwitch01", 1);

			}

			public float avgLen() {
				float sum = 0;
				for (int i = 0; i< targets.Count; i++) sum += source.distance(targets[i]) - source.ceasePullDistance() ;
				return sum/targets.Count;
			}

			public override void accion() {
				if (source.getState ("nearFarSwitch01") == 1){ //can be suppressed by setting nearFarSwitch01 to 0
					float avgLn = avgLen();
					if (avgLn <= near) {
						source.setState ("push1Pull2", 1);
						priorAvgLn = -1000;
					} else if (avgLn >= far) {
						source.setState ("push1Pull2", 2);
						priorAvgLn = -1000;
					} else if (Mathf.Abs (avgLn - priorAvgLn) < avgLn / 100) { //is between near and far, but not moving much, is perhaps too weak to make full range against bone
						source.setState ("push1Pull2", source.getState ("push1Pull2") == 1 ? 2 : 1);
						priorAvgLn = -1000;
					} else priorAvgLn = avgLn;
				}
			}

		}
		


		public class BonePullServo: Rule {

			public static void install(Node source, List<Node> targetList){
				if (source == null || targetList.Contains (null)) return;

				source.rules.Add (new BonePullServo(source, targetList));
			}

			int priorPushPull;
			List<Node> sourceList, targetList;

			private BonePullServo( Node source0, List<Node> targetList0):base(source0){

				targetList = targetList0;

				priorPushPull = 0;

				for (int i=0; i< targetList.Count; i++) addMuscle(targetList[i]);

				for (int i=0; i< musclesCount; i++) muscles(i).makePuller().enable(300);

				sourceList = nodeList(source);

				source.setState ("push1Pull2",2);
				source.setState ("forward0Reverse1",0);
				source.setState ("turn", 0); //so that getState("turn") is never undefined
			}

			public override void accion() {

				//suppress during turns, but ignore turn if an inchworm
				if (musclesCount < 2 || source.getState("turn") == 0) { 

					int push1Pull2;
					int forwardReverse;

					push1Pull2 = source.getState("push1Pull2"); //used by manual onPush1Pull2 or automatic nearFarPush1Pull2Cmdr to control this rule

					if (push1Pull2 == 0) { 
						priorPushPull = 0;
						disableMuscles();
						return; 
					}

					if (push1Pull2 != priorPushPull) {
						
						priorPushPull = push1Pull2;
						forwardReverse = source.getState ("forward0Reverse1");

						if (push1Pull2 == 1 ){
							disableMuscles (); //let bone push back to starting length
							Org.shiftBurden(forwardReverse,sourceList,targetList);

						}else if (push1Pull2 == 2){
							reEnableMuscles(); //pull compress bone
							Org.shiftBurden(1-forwardReverse,sourceList,targetList);
						}
					}

					//Pushing naturally tends to equalize isoceles triangle--shorter link is more efficient, so pushes more.
					//Pulling has the opposite effect, so need to counteract
//					if (musclesCount >= 2 && push1Pull2 == 2 ) {
//						if (muscles(0).efficiency() > muscles(musclesCount-1).efficiency()){ 
//							muscles(0).disable(); muscles(musclesCount-1).reEnable ();
//						} else {
//							muscles(0).reEnable(); muscles(musclesCount-1).disable ();
//						}
//					}ß
				} else { disableMuscles(); priorPushPull = 0; } //so will reinitialize pushPull after turn is finished
			}

		}



		public abstract class HunterBase: Rule {
			
			public enum State {notFighting, fleeing, attacking};

			protected Muscle fightingMuscle;
			
			protected bool amTargeting(Node target) {
				return fightingMuscle.enabled && fightingMuscle.target == target  ;
			}
			
			protected State state(){
				if (fightingMuscle.disabled) return State.notFighting;
				if (fightingMuscle.isPuller()) return State.attacking;
				return State.fleeing;
			}
			
			protected void startFighting(Node target, State newState){ //newState must be either attacking or fleeing
				fightingMuscle.reTarget(target); //whether or not it already was
				fightingMuscle.enable(100);
				
				if (newState == State.attacking) fightingMuscle.makePuller ();
				else if (newState == State.fleeing) fightingMuscle.makePusher ();
			}
			
			protected void stopFighting(){
				fightingMuscle.disable();
			}

			abstract override public void accion();

			public HunterBase(Node source0):base(source0){
			}
		}

		public class cowardNPCRule: HunterBase{

			public static void install(Node source0){
				if (source0 == null) return;
				source0.rules.Add (new cowardNPCRule(source0));
			}

			private cowardNPCRule(Node source0):base(source0){

				fightingMuscle = addMuscle(source0); // a cut muscle, disabled

			}

			override public void accion(){

				if (source.mounted()){ fightingMuscle.cut(); return;} //disabled while org is mounted by human

				Node bully;

				bully = source.mostDangerousNeighbor(); //may be null

				if (state() == State.fleeing && !amTargeting(bully)) stopFighting(); //quit fleeing on bully == null, or shift to new danger

				//regardless of state, flee the most dangerous neighbor, unless you already are...
				if ( bully != null && !(state() == State.fleeing && amTargeting(bully))){
					stopFighting(); 
					startFighting(bully, State.fleeing); 
				}

			}
		}

					

		public class HunterNPCRule: HunterBase{

			public static void install(Node source0){
				if (source0 == null) return;
				source0.rules.Add (new HunterNPCRule(source0));
			}

			private HunterNPCRule(Node source0):base(source0){

				fightingMuscle = addMuscle(source0); // a cut muscle, disabled
				
			}

			override public void accion(){

				//stop hunting while towing a prisoner to goal, so won't interfere with goalSeeker
				if (source.mounted() || source.org.hasPrisoner()){ fightingMuscle.cut(); return;} //disabled while org is mounted by human

				Node bully, munchies;

				munchies = source.mostTastyNeighbor(); //may be null
				
				if (state() == State.attacking && !amTargeting(munchies)) stopFighting(); //quit attacking, or shift to new opportunity
				
				if (state() == State.notFighting && munchies != null ) startFighting(munchies, State.attacking);
				//note: the above never interrupt fleeing
				
				bully = source.mostDangerousNeighbor(); //may be null
				
				if (state() == State.fleeing && !amTargeting(bully)) stopFighting(); //quit fleeing on bully == null, or shift to new danger
				
				//regardless of state, flee the most dangerous neighbor, unless you already are...
				if ( bully != null && !(state() == State.fleeing && amTargeting(bully))){
					stopFighting(); 
					startFighting(bully, State.fleeing); 
				}
				
			}
		}

					

		public class HunterPCRule: HunterBase{

			public static void install(Node source, byte hand){
				if (source == null) return;
				source.rules.Add (new HunterPCRule(source, hand));
			}

			const int idle = int.MaxValue;
			string handTargetIdp1;

			//side effect: adds a rule, to source0, such that it is responsive to targetIdp1 state
			private HunterPCRule(Node source0, byte hand):base(source0){

				handTargetIdp1 = hand + "targetIdp1";

				fightingMuscle = addMuscle(source0); //a cut muscle, disabled
				
				source.setState(handTargetIdp1,idle);
				
			}


			override public void accion(){
				int step;
				int targetIdp1 = source.getState(handTargetIdp1); // push+, pull-, (targetId+1). 0 means stop fighting
				//AND idle means a previous accion has already dealt with the message.
				if (targetIdp1 == idle) return;
				source.setState (handTargetIdp1,idle);

				if (targetIdp1 == 0) { 
					stopFighting(); 
					return; 
				}
				
				Node target = Engine.nodes[ targetIdp1<0? -1 - targetIdp1:targetIdp1-1 ];

				if (source.org == target.org ) {
					stopFighting(); // removed || source.clan == target.clan so clan members can pull or push each other out of danger etc.
					return;
				}

				if (fightingMuscle.disabled) { // state() == State.notFighting
					startFighting(target, targetIdp1>=0?State.fleeing:State.attacking);
					return;
				}
				
				if (amTargeting(target)) {

					if (fightingMuscle.isPuller() && source.overlaps (target)) {
						stopFighting();
						return;
					}

					step = fightingMuscle.enabledStep(); //0,1,2,3
					if (fightingMuscle.isPuller()) step = -step;
					//step is now in -3..3
					step += targetIdp1<0?-1:1; //move step left or right, is now in -4..4

					if (step == 0) stopFighting();
					else if (step < 0) {
						fightingMuscle.makePuller ();
						fightingMuscle.enable (-step*100); //percent
					} else {
						fightingMuscle.makePusher ();
						fightingMuscle.enable (step*100); //percent
					}
				}
				
				if (source.org == target.org) stopFighting(); // removed || source.clan == target.clan so clan members can pull or push each other out of danger etc.
				
				else startFighting(target, targetIdp1>=0?State.fleeing:State.attacking);
				
			}

		}


		//unused
		public class SegmentPushPullServo: Rule {

			//tapeworm segment, muscles go from head to tail, last tail should be small because it has no one to give its burden to.
			public static void install(Node source0, Node target0, int n){
				if (source0 == null || target0 == null ) return;
				source0.rules.Add (new SegmentPushPullServo(source0, target0, n));
			}

			Node target;
			int priorPushPull = 0;
			
			private SegmentPushPullServo( Node source0, Node target0, int n):base(source0){
				target = target0; //next in line
				addMuscle(target);
				source.setState ("push1Pull2", n%2==0?2:1); //set initial state alternately
			}
			
			public override void accion() {

				if (source.org.isStrippedServant ()) return; //can't use offload/restore burden when stripped
					
				int forward0Reverse1; //for now simplify things
				int push1Pull2 = source.getState ("push1Pull2");

				if (push1Pull2 == 0) { 
					priorPushPull = 0;
					disableMuscles();
					return; 
				}
				if (push1Pull2 != priorPushPull) {
					reEnableMuscles();
					priorPushPull = push1Pull2;
					forward0Reverse1 = source.getState ("forward0Reverse1");

					if (push1Pull2 == 1) {
						if (forward0Reverse1 == 0) source.offloadBurden(); else source.restoreNaiveBurden();
						muscles(0).makePusher();

					} else if (push1Pull2 == 2) {
							if (forward0Reverse1 == 0) source.restoreNaiveBurden(); else source.offloadBurden();
						muscles(0).makePuller();
					}
				}
	//			bubbleServer.debugDisplay (
	//				source.id+" "+ push1Pull2+(muscles(0).isPuller()?"pulls ":"pushes ")+target.id+" "
	//				+ source.burden +":"+target.burden);
				                          
			}
		}



		
		public class SegmentPulltokenServo: Rule {

			//tapeworm segment, muscles go from head to tail, last tail should be small because it has no one to give its burden to.
			public static void install(Node source0, Node target0){
				if (source0 == null || target0 == null) return;
				source0.rules.Add (new SegmentPulltokenServo(source0, target0));
			}

			Node target;
			Muscle muscl; 
			
			private SegmentPulltokenServo( Node source0, Node target0):base(source0){
				target = target0; //next in line
				muscl = addMuscle(target); //muscl is convenience for muscles(0)
				if (source.org.head == source){ //pulling starts with org head
						source.setState("push1Pull2",2);
				}
				else source.setState("push1Pull2",0);
			}

			//someday add forward0Reverse1 to these considerations, will reverse giveBurden/retakeBurden
			public override void accion() {

				if (source.org.isStrippedServant ()) return; //can't use offload/restore burden when stripped

				int push1Pull2 = source.getState ("push1Pull2");

				if (push1Pull2 == 0){ muscl.disable(); return; }

				if (push1Pull2 == 2) {

					//I am the puller. Check for transition
					if (muscl.relativeLength() < 1) {
						target.restoreNaiveBurden();
						source.setState("push1Pull2",0);
						//pass the token to next, but not to the tail which has no rule at all
						if (target.getState("push1Pull2")==0){
							target.setState("push1Pull2",2);
						} else {
							source.org.head.setState("push1Pull2",1); //set head to pushing!
						}
					}
					else { //normal pulling
						target.offloadBurden(); //make target light
						muscl.makePuller().reEnable();
					}
					return;
				}

				if (push1Pull2 == 1){ //only the head pushes

					//check for transition to pull
					if (muscl.relativeLength () > 10){
						source.restoreNaiveBurden();
						source.setState("push1Pull2",2);

					}
					else { //normal pushing
						source.offloadBurden ();
						muscl.makePusher().reEnable();
					}
				}
			}
		}


		public class TurmDefender: Rule {


			public static void install(Node source0, float perimeter){
				if (source0 == null) return;
				source0.rules.Add (new TurmDefender(source0, perimeter));
			}

			private Muscle pusher;
			private float perimeter;

			private TurmDefender(Node source0, float perimeter0):base(source0){
				perimeter = perimeter0;
				pusher = addMuscle(source0).makePusher(); //a cut muscle, disabled
				//pusher just convenience for muscles(0)
			}

			override public void accion(){

				if (source.mounted()){ pusher.cut(); return;} //disabled while org is mounted by human

				Node targt = source.closestStranger ();
				if (targt == null || source.distance (targt) > perimeter ) pusher.cut();
				else {
					pusher.reTarget(targt); 
					pusher.enable(100);
				}
			}
		}



		public class Crank: Rule {

			//install on two nodes held at 60-120 degrees from each other about the center, cranking the given crank node
			public static void install(Node source,  Node center, Node crank, bool cwise){
				if (source == null || center == null || crank == null) return;
				source.rules.Add (new Crank(source, center, crank, cwise));
			}
			
			private Muscle muscl;
			private Node crank;
			private Node center;
			private bool cwise;
			
			private Crank(Node source0, Node center0, Node crank0, bool cwise0):base(source0){
				center = center0;
				crank = crank0;
				cwise = cwise0;
				muscl = addMuscle(crank).disable(); //source to crank. Disable so it remembers enablement for reEnable.
			}
				

			override public void accion(){
				float radius2 = source.distance2(center);
				float dist2 = source.distance2(crank);
				float angl = signedAngle(center,source,crank);
				if (Mathf.Abs(angl)<Mathf.PI/9 ) muscl.disable();  // dist = 2*radius at farthest point on circumscribed circle
				else {
					if ( dist2 < radius2/10000) muscl.disable();//note the condition is the same as dist < radius/100
					else {
						muscl.reEnable();
						if ( cwise ^ (signedAngle(center,source,crank) > 0 )) muscl.makePuller(); 
						else muscl.makePusher();
					}
				}
			}
		}


		//steers organism by pushing or pulling on nodes to left or right of direction of travel, to steer organism toward the goal.
		//can only be installed on org head.
		//only works in forward! Don't know what effects will be in reverse...
		public class GoalSeeker: Rule {

			public static void install(Node source0, Node goal0, bool onlyWhileHavePrisoner0 = false){
				if (source0 == null || source0.org.head != source0 || goal0 == null) return;
				source0.rules.Add (new GoalSeeker(source0, goal0, onlyWhileHavePrisoner0));
			}

			private delegate bool NoArgBool();

			private Muscle muscl;
			private Node goal;
			private NoArgBool suppressed;

			private bool f(){ return false;}
			private bool noPrisoner(){ return !source.org.hasPrisoner ();}

			private GoalSeeker(Node source0, Node goal0, bool onlyWhileHavePrisoner):base(source0){
				goal = goal0;
				muscl = addMuscle(source0); //a cut muscle, disabled
				//muscle just convenience for muscles(0)

				if (onlyWhileHavePrisoner) suppressed = noPrisoner;
				else suppressed = f;
			}

			override public void accion(){
				
				if (source.mounted() || suppressed() ){ muscl.cut(); return;} //disabled while org is mounted by human, or if org is not trailing a prisoner

				float angleToGoal = signedAngle(goal, source, source.org.COB());
				angleToGoal = angleToGoal>0?Mathf.PI-angleToGoal:-(Mathf.PI+angleToGoal); //does not change sign of angleToGoal
				
				SteeringStruct ss = source.bestSteeringNeighbor();
	//			ss.sideEffect *= source.naiveBurden/source.burden; //muscle power (demand) will have more effect if burden's been shifted off me
		
				if (ss.target == null || ss.sideEffect < 0.01) muscl.disable();
				else {
					if (muscl.target != ss.target) muscl.reTarget(ss.target);
					if (ss.sideEffect*angleToGoal > 0) muscl.makePuller(); else muscl.makePusher();
					muscl.enable(Mathf.RoundToInt(46*Mathf.Abs(angleToGoal/ss.sideEffect))); //coefficient tuned
				}
			}
		}

		public class FullGoalScore: Rule {

			public static void install(Node source0){
				if (source0 == null || source0.org.head != source0 ) return;
				source0.rules.Add (new FullGoalScore(source0));
			}

			private FullGoalScore(Node source0):base(source0){
				source.oomph = source.maxOomph/5; // this default may be overridden in Bots

			}
				

			private Node leastSupplied(){
				List<Node> myTeam = Bots.teams[source.teamNumber];
				Node worstSupplied = myTeam[0];
				for (int i = 1; i < myTeam.Count; i++) {
					if (myTeam[i].fuelGauge < worstSupplied.fuelGauge ) worstSupplied = myTeam[i];
				}
				return worstSupplied;
			}

			private void helpTheNeedy(){
				Node worstSupplied = leastSupplied ();
				if (worstSupplied.fuelGauge < source.fuelGauge) {
					float fairfuelGauge = (worstSupplied.fuelGauge + source.fuelGauge)/2;
					float mostWorstShouldGet = worstSupplied.maxOomph*fairfuelGauge - worstSupplied.oomph; //would raise his fuelGauge to fairfuelGauge level
					float mostIShouldGive = source.oomph - fairfuelGauge * source.maxOomph; //would drop my fuelGauge to fairfuelGauge level
					float oomphToTransfer = Mathf.Min(mostWorstShouldGet, mostIShouldGive);
					source.oomph -= oomphToTransfer;
					worstSupplied.oomph += oomphToTransfer*Node.linkEfficiency(source, worstSupplied); //with love. Less will be actually transfered, because of efficiency
				}
			}

			override public void accion(){

				if (source.oomph > source.maxOomph * 0.99f) {
					Score.scoreTeamWin (source.teamNumber);
					bubbleServer.newRound = true;
				} else if (source.oomph < source.maxOomph * 0.01f){
					Score.scoreTeamLoss(source.teamNumber);
					bubbleServer.newRound = true;
				} else {
					helpTheNeedy ();
				}
			}
		}

		public class TouchGoalScore: Rule {

			public static void install(Node source0){
				if (source0 == null || source0.org.head != source0 ) return;
				source0.rules.Add (new TouchGoalScore(source0));
			}

			private TouchGoalScore(Node source0):base(source0){
			}

			override public void accion(){
				
				int myTeamNumber = source.teamNumber;

				for (int j = 0; j < source.site.neighborsCount (); j++) {
					Node nbr = source.site.neighbors (j);
					int hisTeamNumber = nbr.teamNumber;
					if (hisTeamNumber !=0 && hisTeamNumber != myTeamNumber && source.overlaps(nbr)) {
						Score.scoreTeamWin (hisTeamNumber);
						bubbleServer.newRound = true;
					}
				}
			}
		}


		public class BlessGoal: Rule {

			public static void install(Node source0, Node goal0){
				if (source0 == null || source0.org.head != source0 || goal0 == null) return;
				source0.rules.Add (new BlessGoal(source0, goal0));
			}

			private Node goal;

			private BlessGoal(Node source0, Node goal0):base(source0){
				goal = goal0;
			}

			override public void accion(){

				if (source.mounted ()) return;

				if ( source.fuelGauge > goal.fuelGauge || source.org.oomph() > goal.maxOomph - goal.oomph ) source.bless (goal);

			}
		}

		//When master attains the goal, transfers all of master's prisoners to the goal
		public class GivePrisoners: Rule {
			public static void install(Org master, Org  goal){
				if (master == null || goal == null || master == goal) return;
				GivePrisoners gpRule = new GivePrisoners (master,goal);
				master.head.rules.Add (gpRule);
			}

			private Org master, goal;

			private GivePrisoners(Org master0, Org goal0):base(master0.head){
				master = master0;
				goal = goal0;
			}
				
			override public void accion(){
				if (master.head.overlaps (goal.head)) {
					List<Node> prisoners = master.prisoners ();
					master.liberatePrisoners ();
					foreach (var prisoner in prisoners) {
						goal.takePrisoner (prisoner.org);
						Score.scoreBlessing (master.head.id, prisoner.naiveBurden);
					}
					
				}
			}
		}

	}
}