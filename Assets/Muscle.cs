//copyright 2016 John Fairfield

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bubbles{
public class Muscle {
	
	public static float baseMetabolicRate = 0.0035f;

	public Node source { get; protected set; }
	public Node target { get; private set; }

	public float demand {get; private set;}
	private float pastDemand;

	public bool external {get{ return target.org != source.org;}} // a cut muscle is not external.

	public void reTarget(Node n) {
		if (external) target.org.enemyMuscles.Remove(this); 
		target = n;
		if (external) target.org.enemyMuscles.Add(this); 
	}

	public bool enabled {get{ return demand > 0;}} 
	public bool disabled { get {return demand == 0;}}

	// cut implies disabled, but disabled does not imply cut.
	public Muscle cut() { reTarget(source); pastDemand = demand = 0; return this;}
	public bool notCut { get {return target != source;}}

	public Muscle enable(int percent){
		if ( notCut && percent>=0 && percent <= 300 ) {
			demand = source.radius2 * baseMetabolicRate * percent;//some day * CScommon.testBit(source.dna, CScommon.strengthBit)?10:1; 
			pastDemand = demand; //so that a subsequent reEnable will do nothing.
		}
		return this;
	}

	public Muscle disable(){ if (demand > 0) pastDemand = demand; demand = 0; return this; }
	public Muscle reEnable(){ if (notCut) demand = pastDemand; return this;}

	public int enabledStep(){
		return Mathf.RoundToInt(demand/(source.radius2 * baseMetabolicRate * 100)); //should be 0, 1, 2, or 3
	}

	private bool pulling;
	public bool isPuller() {return pulling;}
	public bool isPusher() { return !pulling;}
	public Muscle makePuller() {pulling = true; return this;}
	public Muscle makePusher() {pulling = false; return this;}
	public Muscle togglePushPull() {pulling = !pulling; return this;}
	public CScommon.LinkType commonType() {return pulling?CScommon.LinkType.puller:CScommon.LinkType.pusher;}

	public Muscle( Node source0, Node target0) {
		source = source0; //may not be null
		target = target0; //may not be null
		//is cut if source == target
		if (notCut) { 
			enable(100); 
			if (external) target.org.enemyMuscles.Add(this); 
		}
		pulling = true;
	}

	public float length(){ return source.distance(target); }
	public float length2() { return source.distance2(target);}
	public float relativeLength(){ return source.distance (target)/source.radius; }
	public float efficiency(){ return CScommon.efficiency(source.distance2(target),source.radius2); }

	//the oomph that this particular muscle can actually put to work (some is wasted in inefficiency)
	// units: is oomp is d*b

	public float strength() { 
		return demand*CScommon.efficiency(length2(),source.radius2);
	}

	//		// friction narrative: the force of a link operates independently on both ends. 
	//		// A better way of putting it: each end gets half the oomph expended on it. That way, the amount one end moves
	//		// is independent of how much the other end moves--it all depends on their individual grips.
	//		// The unit of oomph is a grip meter
	//		// So total amount of lengthening or shortening is
	//		private float oomphToDisplacement(float omp){
	//			return 0.5f*omp*(1/source.grip + 1/target.grip);
	//		}
	//		
	//		private float displacementToOomph(float disp){
	//			return 2*disp/(1/source.grip + 1/target.grip);
	//		}


	// must not debit oomph finally until all muscles have partaken, else later muscles would have less oomph than earlier muscles
	public float actionDemand()
	{	if (isPuller() && length() < source.ceasePullDistance()) return 0;
		return demand;
	}

	// Executes the muscle action given sources's perhaps limited ability to power the muscle
	public void action(float fraction){
		float dx, dy, deliveredOomph, displacement, effect, len = length();

		if (len==0) return;
		if (isPuller() && len <= source.ceasePullDistance()) return;
		if ( fraction*demand == 0) return;

		dx = target.x - source.x;
		dy = target.y - source.y;

		deliveredOomph = fraction*demand*efficiency();
		if (deliveredOomph <= 0) return;

		// friction narrative: the force of a link operates independently on both ends. 
		// A better way of putting it: energy of action == energy of reaction, i.e.
		// each end gets half the oomph expended on it. That way, the amount one end moves
		// is independent of how much the other end moves--it all depends on their individual grips.
		// The unit of oomph is a grip meter

		// Effect on one end is independent of effect on the other.
		// Each experiences the same 'force', they react to it in inverse proportion to their grip.
		// oomph is displacement of a unit grip,
		// but in general both ends don't have unit grip
		// Net: gap between them will shorten (puller) by displacement, where
		displacement = 0.5f*deliveredOomph/source.grip + 0.5f*deliveredOomph/target.grip;

		// insure that puller doesn't overshoot zero.
		// Further, ensure that length stays north of an epsilon:
		// if length gets too close to zero, numerical effects can result in node superposition
		// Further, ensure that puller never pulls you closer than you need to eat, to keep nodes from being
		// so confounded that it's difficult for players to distinguish them.
		// Also, perfect superposition is hard on voronoi, and 
		// you can't start pushing on it with any notion of direction of push.

		if (isPuller()){ 
			//note that at this point we have len > ceasePullDistance()
			if ( displacement > len - source.ceasePullDistance()) {
				float x = (len-source.ceasePullDistance())/displacement;
				deliveredOomph *= x;
				displacement *= x;
			}
			// give a sign to oomph-- pos if pushing (trying to increase length), neg if pulling
			deliveredOomph = -deliveredOomph; 
			displacement = -displacement;
		}

		source.pushedMinusPulled += displacement; // accumulate positive if pushed, negative if pulled
		target.pushedMinusPulled += displacement; // gravity question: should this be weighted (as below) by inverse grip?

		//change units from oomph = grip*meter to grip*meter/meter, i.e. grip * fraction of length, and cut it in half to equally apply it to both ends
		effect = deliveredOomph/(2*len); //so effect/grip is dimensionless

		// has full effect on unit grip
		//A smaller grip moves more than a bigger grip.
		source.nx -= dx*effect/source.grip;
		source.ny -= dy*effect/source.grip;

		target.nx += dx*effect/target.grip;
		target.ny += dy*effect/target.grip;

	}
	}}