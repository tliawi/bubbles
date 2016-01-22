using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Rules {

	//given an angle in radians, returns the equivalent between -PI and +PI
	public static float stdAngle( float angl)
	{	while (angl < -Mathf.PI) angl += 2*Mathf.PI;
		while (angl > Mathf.PI) angl -= 2*Mathf.PI;
		return angl;
	}
	
	// Angle abc. Given three points a,b,c
	// returns the angle at the middle one, b.
	// Is positive if angle from a to b to c is ccwise(standard math positive angle direction), 
	// negative if cwise.
	public static float signedAngle( Bub.Node a, Bub.Node b, Bub.Node c)
	{	float angle1 = Mathf.Atan2(a.y-b.y, a.x-b.x);
		float angle2 = Mathf.Atan2(c.y-b.y, c.x-b.x);
		return stdAngle(angle2 - angle1);
	}

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

	public static List<Bub.Node> nodeList(Bub.Node nod0, Bub.Node nod1, Bub.Node nod2){
		List<Bub.Node> lst =  new List<Bub.Node>();
		lst.Add (nod0);
		lst.Add (nod1);
		lst.Add (nod2);
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

		public void disableMuscles(){
			for (int i = 0; i< _muscles.Count; i++) _muscles[i].disable ();
		}

		public void enableInternalMuscles(int percent)
		{	for (int i = 0; i< _muscles.Count; i++) if (!_muscles[i].external && source.trusts(_muscles[i].target)) _muscles[i].enable (percent);
		}

		public void enableMuscles(int percent){
			for (int i = 0; i< _muscles.Count; i++) _muscles[i].enable (percent);
		}

		public void reEnableMuscles(){
			for (int i = 0; i< _muscles.Count; i++) _muscles[i].reEnable ();
		}

		public void cutExternalMuscles(){
			for (int i=0; i< _muscles.Count; i++) if (_muscles[i].external ) _muscles[i].cut();
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

		protected Rule(Bub.Node source0) {
			source = source0;
			_muscles = new List<Bub.Muscle>();
		}
	}
	


	//disabled by source.setState("turn",0)
	public class TurnServo: Rule{

		public static void install(Bub.Node source, Bub.Node firstTarget, Bub.Node lastTarget){
			if (source == null || firstTarget == null || lastTarget == null) return;
			
			source.rules.Add (new TurnServo(source, firstTarget, lastTarget));
		}

		int countDown;
		List<Bub.Node> sourceList;
		List<Bub.Node> targetList;

		private TurnServo(Bub.Node source0, Bub.Node firstTarget, Bub.Node lastTarget):base(source0){

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
			Bub.Node x = muscles(0).target;
			muscles(0).reTarget(muscles(1).target);
			muscles(1).reTarget(x);
		}

		public override void accion() {

			if (signedAngle(muscles(1).target,muscles(0).target,source)<0) flipHandedness();

			int turn = source.getState ("turn"); //-1, 0, +1, 12345 (during a turn)

			if (turn == 0 ) { disableMuscles(); countDown = 0; return;}//end turn early....
			
			if (turn == 1 || turn == -1) { //starting a turn
				source.setState ("turn", 12345); //signal that I am doing a turn

				Bub.shiftBurden (0,sourceList,targetList);
				
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
		
		public static void install(Bub.Node source, List<Bub.Node> targetList){
			if (source == null || targetList.Contains (null)) return;
			
			source.rules.Add (new Push1Pull2Servo(source, targetList));
		}
	
		int priorPushPull;
		List<Bub.Node> sourceList, targetList;

		private Push1Pull2Servo( Bub.Node source0, List<Bub.Node> targetList0):base(source0){

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
						muscles(0).disable(); muscles(musclesCount-1).reEnable ();
					} else {
						muscles(0).reEnable(); muscles(musclesCount-1).disable ();
					}
				}
			} else { disableMuscles(); priorPushPull = 0; } //so will reinitialize pushPull after turn is finished
		}

	}



	
	//Controls push1Pull2Servo via push1Pull2 state.
	//Integer versions of Near and Far could be kept in state if we want to make this rule modifyable by other rules or actions.
	public class NearFarPush1Pull2Cmdr:Rule {

		//A Servo is a rule with muscles.
		//A Cmdr is a rule with musclesCount == 0 that takes the place of a user via manipulating state only, thereby controlling servo rules
		//near and far are in terms of link gap, i.e. distance - sum of radii of source and target
		public static void install(Bub.Node source0, List<Bub.Node> targets0, float near0, float far0){
			if (source0 == null || targets0.Contains (null)) return;
			source0.rules.Add (new NearFarPush1Pull2Cmdr(source0, targets0, near0, far0));
		}

		float near, far;
		List<Bub.Node> targets;

		private NearFarPush1Pull2Cmdr(Bub.Node source0, List<Bub.Node> targets0, float near0, float far0):base(source0) {
		
			targets = targets0;
			near = near0;
			far = far0;

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
				if ( avgLn <= near) source.setState ("push1Pull2",1);
				else if (avgLn >= far) source.setState ("push1Pull2",2);
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
			fightingMuscle.reTarget(target); //whether or not it already was
			fightingMuscle.enable(100);
			
			if (newState == State.attacking) fightingMuscle.makePuller ();
			else if (newState == State.fleeing) fightingMuscle.makePusher ();
		}
		
		protected void stopFighting(){
			fightingMuscle.disable();
		}

		abstract override public void accion();

		public HunterBase(Bub.Node source0):base(source0){}
	}


				

	public class HunterNPCRule: HunterBase{

		public static void install(Bub.Node source0){
			if (source0 == null) return;
			source0.rules.Add (new HunterNPCRule(source0));
		}

		private HunterNPCRule(Bub.Node source0):base(source0){

			fightingMuscle = addMuscle(source0); // a cut muscle, disabled
			
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

				

	public class HunterPCRule: HunterBase{

		public static void install(Bub.Node source, byte hand){
			if (source == null) return;
			source.rules.Add (new HunterPCRule(source, hand));
		}

		const int idle = int.MaxValue;
		string handTargetIdp1;

		//side effect: adds a rule, to source0, such that it is responsive to targetIdp1 state
		private HunterPCRule(Bub.Node source0, byte hand):base(source0){

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
			
			Bub.Node target = Engine.nodes[ targetIdp1<0? -1 - targetIdp1:targetIdp1-1 ];

			if (source.trusts(target) ) {
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
			
			if (source.trusts (target) ) stopFighting(); // removed || source.clan == target.clan so clan members can pull or push each other out of danger etc.
			
			else startFighting(target, targetIdp1>=0?State.fleeing:State.attacking);
			
		}

	}



	public class SegmentPushPullServo: Rule {

		//tapeworm segment, muscles go from head to tail, last tail should be small because it has no one to give its burden to.
		public static void install(Bub.Node source0, Bub.Node target0, int n){
			if (source0 == null || target0 == null ) return;
			source0.rules.Add (new SegmentPushPullServo(source0, target0, n));
		}

		Bub.Node target;
		int priorPushPull = 0;
		
		private SegmentPushPullServo( Bub.Node source0, Bub.Node target0, int n):base(source0){
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
				reEnableMuscles();
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



	
	public class SegmentPulltokenServo: Rule {

		//tapeworm segment, muscles go from head to tail, last tail should be small because it has no one to give its burden to.
		public static void install(Bub.Node source0, Bub.Node target0){
			if (source0 == null || target0 == null) return;
			source0.rules.Add (new SegmentPulltokenServo(source0, target0));
		}

		Bub.Node target;
		
		private SegmentPulltokenServo( Bub.Node source0, Bub.Node target0):base(source0){
			target = target0; //next in line
			addMuscle(target);
			if (source.trustHead == source){ //pulling starts with trustHead
					source.setState("push1Pull2",2);
			}
			else source.setState("push1Pull2",0);
		}

		//someday add forward0Reverse1 to these considerations, will reverse giveBurden/retakeBurden
		public override void accion() {
			int push1Pull2 = source.getState ("push1Pull2");

			if (push1Pull2 == 0){ muscles(0).disable(); return; }

			if (push1Pull2 == 2) {

				//I am the puller. Check for transition
				if (muscles(0).relativeLength() < 1) {
					target.retakeBurden();
					source.setState("push1Pull2",0);
					//pass the token to next, but not to the tail which has no rule at all
					if (target.getState("push1Pull2")==0){
						target.setState("push1Pull2",2);
					} else {
						source.trustHead.setState("push1Pull2",1); //set head to pushing!
					}
				}
				else { //normal pulling
					target.giveBurden(source);
					muscles(0).makePuller().reEnable();
				}
				return;
			}

			if (push1Pull2 == 1){ //only the head pushes

				//check for transition to pull
				if (muscles(0).relativeLength () > 10){
					source.retakeBurden();
					source.setState("push1Pull2",2);

				}
				else { //normal pushing
					source.giveBurden (target);
					muscles(0).makePusher().reEnable();
				}
			}
		}
	}


	public class TurmDefender: Rule {

		public static void install(Bub.Node source0, float perimeter){
			if (source0 == null) return;
			source0.rules.Add (new TurmDefender(source0, perimeter));
		}

		private Bub.Muscle pusher;
		private float perimeter;

		private TurmDefender(Bub.Node source0, float perimeter0):base(source0){
			perimeter = perimeter0;
			pusher = addMuscle(source0).makePusher(); //a cut muscle, disabled
			//pusher just convenience for muscles(0)
		}

		override public void accion(){
			Bub.Node targt = source.closestStranger ();
			if (targt == null || source.distance (targt) > perimeter ) pusher.cut();
			else {
				pusher.reTarget(targt); 
				pusher.enable(100);
			}
		}
	}



	public class Crank: Rule {

		//install on two nodes held at 60-120 degrees from each other about the center, cranking the given crank node
		public static void install(Bub.Node source,  Bub.Node center, Bub.Node crank, bool cwise){
			if (source == null || center == null || crank == null) return;
			source.rules.Add (new Crank(source, center, crank, cwise));
		}
		
		private Bub.Muscle muscl;
		private Bub.Node crank;
		private Bub.Node center;
		private bool cwise;
		
		private Crank(Bub.Node source0, Bub.Node center0, Bub.Node crank0, bool cwise0):base(source0){
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
}