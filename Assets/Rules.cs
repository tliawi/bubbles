using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Rules {

	//overloaded helper fcn
	public static List<Bub.Node> nodeList(Bub.Node nod0){
		List<Bub.Node> lst =  new List<Bub.Node>();
		lst.Add (nod0);
		return lst;
	}
	
	public static List<Bub.Node> nodeList(Bub.Node nod0, Bub.Node nod1){
		List<Bub.Node> lst =  new List<Bub.Node>();
		lst.Add (nod0);
		lst.Add (nod1);
		return lst;
	}

	public abstract class Rule {

		public Bub.Node source {get; protected set;}

		private List<Bub.Muscle> _muscles;

		public Bub.Muscle muscles( int i) { return _muscles[i]; }
		
		public int musclesCount { get{return _muscles.Count;} }

		protected Bub.Muscle addMuscle(Bub.Node target){ 
			Bub.Muscle muscle = new Bub.Muscle(source,target);
			_muscles.Add (muscle);
			return muscle;
		}

		protected void disableMuscles(){
			for (int i = 0; i< _muscles.Count; i++) _muscles[i].disable ();
		}

		protected void enableMuscles(){
			for (int i = 0; i< _muscles.Count; i++) _muscles[i].enable ();
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

		public Rule(Bub.Node source0) {
			source = source0;
			_muscles = new List<Bub.Muscle>();
		}
	}
	
	public static void installTurnServo(Bub.Node source, Bub.Node firstTarget, Bub.Node lastTarget){
		source.rules.Add (new TurnServo(source, firstTarget, lastTarget));
	}

	//disabled by source.setState("turn",0)
	public class TurnServo: Rule{

		int countDown;
		List<Bub.Node> sourceList;
		List<Bub.Node> targetList;

		public TurnServo(Bub.Node source0, Bub.Node firstTarget, Bub.Node lastTarget):base(source0){

			//ASSUMPTION: order of muscles sweeps from L to R,
			addMuscle(firstTarget);
			addMuscle(lastTarget);
			disableMuscles();

			sourceList = nodeList(source);
			targetList = nodeList(firstTarget, lastTarget);

			countDown = 0;
			source.setState ("turn",0);
		}

		public override void accion() {

			int turn = source.getState ("turn"); //-1, 0, +1, 12345 (during a turn)

			if (turn == 0 ) { disableMuscles(); countDown = 0; return;}//end turn early....
			
			if (turn == 1 || turn == -1) { //starting a turn
				source.setState ("turn", 12345); //signal that I am doing a turn

				Bub.shiftBurden (0,sourceList,targetList);
				
				//make muscles work at cross purposes, for elliptical movement (movement of head in an elliptical orbit about tails)
				if (turn < 0) { muscles(0).makePuller(); muscles(1).makePusher();} //L 
				else {muscles(0).makePusher(); muscles(1).makePuller ();} //R

				enableMuscles();

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

	public static void installPush1Pull2Servo(Bub.Node source, List<Bub.Node> targetList){
		source.rules.Add (new Push1Pull2Servo(source, targetList));
	}

	public class Push1Pull2Servo: Rule {
	
		int priorPushPull;
		List<Bub.Node> sourceList, targetList;

		public Push1Pull2Servo( Bub.Node source0, List<Bub.Node> targetList0):base(source0){

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
					enableMuscles();
					priorPushPull = push1Pull2;
					forwardReverse = source.getState ("forward0Reverse1");

					if (push1Pull2 == 1 ){
						Bub.shiftBurden(forwardReverse,sourceList,targetList);
						for (int i=0; i< musclesCount; i++) muscles(i).makePusher();
						
					}else if (push1Pull2 == 2){
						Bub.shiftBurden(1-forwardReverse,sourceList,targetList);
						for (int i=0; i< musclesCount; i++) muscles(i).makePuller();
					}
				}

				//Pushing naturally tends to equalize isoceles triangle--shorter link is more efficient, so pushes more.
				//Pulling has the opposite effect, so need to counteract
				if (musclesCount >= 2 && push1Pull2 == 2 ) {
					if (muscles(0).efficiency() > muscles(musclesCount-1).efficiency()){ 
						muscles(0).disable(); muscles(musclesCount-1).enable ();
					} else {
						muscles(0).enable(); muscles(musclesCount-1).disable ();
					}
				}
			} else { disableMuscles(); priorPushPull = 0; } //so will reinitialize pushPull after turn is finished
		}

	}


	
	//A Servo is a rule with muscles.
	//A Cmdr is a rule with musclesCount == 0 that takes the place of a user via manipulating state only, thereby controlling a servo rule
	
	public static void installNearFarPush1Pull2Cmdr(Bub.Node source0, List<Bub.Node> targets0, float near0, float far0){
		source0.rules.Add (new NearFarPush1Pull2Cmdr(source0, targets0, near0, far0));
	}
	
	//Controls push1Pull2Servo via push1Pull2 state. Near and Far are in relativeLength, i.e length/source.radius
	//Integer versions of Near and Far could be kept in state if we want to make this rule modifyable by other rules or actions.
	public class NearFarPush1Pull2Cmdr:Rule {

		float near, far;
		List<Bub.Node> targets;

		public NearFarPush1Pull2Cmdr(Bub.Node source0, List<Bub.Node> targets0, float near0, float far0):base(source0) {
		
			targets = targets0;
			near = near0;
			far = far0;

			source.setState ("nearFarSwitch01", 1);

		}

		public float avgRelativeLength() {
			float sum = 0;
			for (int i = 0; i< targets.Count; i++) sum += source.relativeDistance(targets[i]);
			return sum/targets.Count;
		}

		public override void accion() {
			if (source.getState ("nearFarSwitch01") == 1){ //can be suppressed by setting nearFarSwitch01 to 0
				float avgRelLen = avgRelativeLength();
				if ( avgRelLen <= near) source.setState ("push1Pull2",1);
				else if (avgRelLen >= far) source.setState ("push1Pull2",2);
			}
		}

	}
	

	public abstract class HunterBase: Rule {
		public enum State {notFighting, fleeing, attacking};

		protected Bub.Muscle fightingMuscle;
		
		protected bool amTargeting(Bub.Node target) {
			return fightingMuscle.enabled && fightingMuscle.target == target  ;
		}
		
		protected State state(){
			if (fightingMuscle.disabled) return State.notFighting;
			if (fightingMuscle.isPuller()) return State.attacking;
			return State.fleeing;
		}
		
		protected void startFighting(Bub.Node target, State newState){ //newState must be either attacking or fleeing
			fightingMuscle.target = target; //whether or not it already was
			fightingMuscle.enable ();
			
			if (newState == State.attacking) fightingMuscle.makePuller ();
			else if (newState == State.fleeing) fightingMuscle.makePusher ();
		}
		
		protected void stopFighting(){
			fightingMuscle.disable();
		}

		abstract override public void accion();

		public HunterBase(Bub.Node source0):base(source0){}
	}


	public static void installHunterNPCRule(Bub.Node source0){
		source0.rules.Add (new HunterNPCRule(source0));
	}			

	public class HunterNPCRule: HunterBase{
		
		public HunterNPCRule(Bub.Node source0):base(source0){

			fightingMuscle = addMuscle(source).disable (); //a disabled muscle temporarily from source to source
			
		}

		override public void accion(){

			Bub.Node bully, munchies;

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

	public static void installHunterPCRule(Bub.Node source0){
		source0.rules.Add (new HunterPCRule(source0));
	}			

	public class HunterPCRule: HunterBase{
		
		//side effect: adds a rule, to source0, such that it is responsive to targetIdp1 state
		public HunterPCRule(Bub.Node source0):base(source0){

			fightingMuscle = addMuscle(source).disable (); //temporary from source to source disabled
			
			source.setState("targetIdp1",0);
			
		}

		override public void accion(){
				
			int targetIdp1 = source.getState("targetIdp1"); // push-, pull+, (targetId+1). 0 means stop fighting
			
			if (targetIdp1 == 0) { stopFighting(); return; }
			
			Bub.Node target = Engine.nodes[ targetIdp1<0? -1 - targetIdp1:targetIdp1-1 ]; 
			
			if (amTargeting(target) && source.overlaps (target)) {
				source.setState ("targetIdp1", 0);
				stopFighting(); 
				return;
			}
			
			if (source.trusts (target) ) stopFighting(); // removed || source.clan == target.clan so clan members can pull or push each other out of danger etc.
			
			else startFighting(target, targetIdp1<0?State.fleeing:State.attacking);
			
		}

	}

	//tapeworm segment, muscles go from head to tail, last tail should be small because it has no one to give its burden to.
	public static void installSegmentPushPullServo(Bub.Node source0, Bub.Node target0, int n){
		source0.rules.Add (new SegmentPushPullServo(source0, target0, n));
	}

	public class SegmentPushPullServo: Rule {

		Bub.Node target;
		int priorPushPull = 0;
		
		public SegmentPushPullServo( Bub.Node source0, Bub.Node target0, int n):base(source0){
			target = target0; //next in line
			addMuscle(target);
			source.setState ("push1Pull2", n%2==0?2:1); //set initial state alternately
		}
		
		public override void accion() {
			int forward0Reverse1; //for now simplify htings
			int push1Pull2 = source.getState ("push1Pull2");

			if (push1Pull2 == 0) { 
				priorPushPull = 0;
				disableMuscles();
				return; 
			}
			if (push1Pull2 != priorPushPull) {
				enableMuscles();
				priorPushPull = push1Pull2;
				forward0Reverse1 = source.getState ("forward0Reverse1");

				if (push1Pull2 == 1) {
					if (forward0Reverse1 == 0) source.giveBurden(target); else source.retakeBurden();
					muscles(0).makePusher();

				} else if (push1Pull2 == 2) {
					if (forward0Reverse1 == 0) source.retakeBurden(); else source.giveBurden(target);
					muscles(0).makePuller();
				}
			}
//			bubbleServer.debugDisplay (
//				source.id+" "+ push1Pull2+(muscles(0).isPuller()?"pulls ":"pushes ")+target.id+" "
//				+ source.burden +":"+target.burden);
			                          
		}
	}


}