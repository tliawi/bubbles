
//////////////////////////////////////////// bot builders ////////////////////////////////

// A node's "smarts" function, if it exists, is run every thread tick.
// Bot makers build the bot in the global array gNodes, 
// so they can be displayed via svg.
// Every node has to have a stomach node--all links out of a node (in its links array), will be powered by its stomach's oomph--
// but any node may have a smarts() function, for the thread, to run links out of that node, or even other links it knows of.

// Bot construction is careful work, you have to build the links out from the stomach, from source to target,
// so that a tree-like walk of links from the stomach can find all the nodes which have the backreference to that stomach.

// A bot may retain indices into it (or other node's) links array (like musclex below), but never direct references to links,
// which must be within exactly one node's links array. The reference in a links array may become undefined (the link has been broken),
// but are never shifted within the array, so that an index into links uniquely determines a particular link.

// "spawn" bot builders may return a node--one can then install an AI to its smarts function.

function spawnBot(){


	/*
	function Kite(){

	  this.stomach = pushRandomNode().setRadius(15).setColor("red");
	  this.tail = pushRandomNode().setRadius(10+Math.random()*10).setColor("red");
	  this.anchor1 = pushRandomNode().setRadius(20).setColor("orange"); 
	  this.anchor2 = pushRandomNode().setRadius(20).setColor("orange");
	  
	  addLinkToBot(this.stomach, this.stomach,this.anchor1).setRestLength(56); // standoff stomach from anchors
	  addLinkToBot(this.stomach, this.stomach,this.anchor2).setRestLength(56); // standoff stomach from anchors
	  addLinkToBot(this.stomach, this.anchor1, this.anchor2).setRestLength(25+Math.random()*20).setRate(0.7); //stiff framework;
	  addLinkToBot(this.stomach, this.anchor1,this.tail).setRestLength(25).setRate(0.7); //stiff framework
	  addLinkToBot(this.stomach, this.anchor2,this.tail).setRestLength(25).setRate(0.7); //stiff framework
	  
	  this.musclex = gLinks.length;
	  addLinkToBot(this.stomach, this.stomach,this.tail).setRestLength(75);

	  //prep thread cycle, start at random place in cycle
	  this.pcCycle = 100; this.pcCount = Math.floor(Math.random()*this.pcCycle);
	  
	}

	//add smarts method to prototype shared by all Kites
	Kite.prototype.smarts = function(){

	  if (this.pcCount == 0) {
		 shiftGrip(0.95,[this.anchor1,this.anchor2],[this.stomach]) // shift grip to anchors
		 tryRestLength(this.musclex,200); //begin thrusting free stomach forward
	  }

	  if (this.pcCount == Math.round(this.pcCycle/2)) {
		 shiftGrip(0.95,[this.stomach],[this.anchor1,this.anchor2]); //shift grip to stomach
		 shiftGrip(0.5+0.45*Math.random(), [this.anchor1],[this.anchor2]); // inconsistent curve to consistently one side
		 tryRestLength(this.musclex,50); //begin pulling anchored stomach back
	  }

	  this.pcCount++; if (!(this.pcCount%this.pcCycle)) { this.pcCount = 0; force.resume(); } //without resume it dies down quickly

	}

	*/

	//given an angle in radians, returns the equivalent between -PI and +PI
	stdAngle = function(angl)
	{	while (angl < -Math.PI) angl += 2*Math.PI;
		while (angl > Math.PI) angl -= 2*Math.PI;
		return angl;
	}

	// Angle abc. Given three Points or objects posessing an x: and a y: (world coordinate or UI coordinate, just not TP coordinate!), 
	// returns the angle at the middle one, b.
	// Is positive if angle from a to b to c is ccwise(standard math positive angle direction), 
	// negative if cwise.
	signedAngle = function(a,b,c)
	{	var angle1 = Math.atan2(a.y-b.y, a.x-b.x),
		angle2 = Math.atan2(c.y-b.y, c.x-b.x);
		return stdAngle(angle2 - angle1);
	}

	function findAngle(p0,p1,p2) //always positive
	{	var a = Math.pow(p1.x-p0.x,2) + Math.pow(p1.y-p0.y,2),
			b = Math.pow(p1.x-p2.x,2) + Math.pow(p1.y-p2.y,2),
			c = Math.pow(p2.x-p0.x,2) + Math.pow(p2.y-p0.y,2);
		return Math.acos( (a+b-c) / Math.sqrt(4*a*b) );
	}

	
	function hanSolo()
	{	var dir = -Math.PI + Math.random()*2*Math.PI, //direction of movement, could be changed by steer
			han = pushRandomNode().setRadius(3).setGrip(gMinGrip).setColor("violet");
			//head.stomach == head

		var hanSoloSmarts = function() // han === this
		{	var leastAbsTheta = 7, leasti = -1, leastsLinkx = -1;

			han.links.length = 0; //delete all links
			for (var i=0;i<han.neighbors.length;i++)
			{	var absTheta = Math.abs(signedAngle({x:han.x+Math.cos(dir), y:han.y+Math.sin(dir)}, han, han.neighbors[i]));
				//want to add at least one puller
				if (absTheta < leastAbsTheta) {leastAbsTheta = absTheta; leasti = i; leastsLinkx = han.links.length;}

				if (absTheta < 0.25*Math.PI) //quarter ahead of you
					han.addPlainLink(han.neighbors[i]).setRate(1).setRestLength(0); //add puller

				else if (absTheta > 0.75*Math.PI) //quarter behind you 
					han.addPlainLink(han.neighbors[i]).setRate(1).setRestLength(han.distance(han.neighbors[i])*9.999); //add pusher
			}


			//want to have at least one puller, and it may need to outpull several pushers
			if (leasti>=0 && leastAbsTheta > 0.75*Math.PI) //wow, all nodes are in quarter behind you!
				han.links[leastsLinkx].setRate(10).setRestLength(0); //it's already been added as a pusher, turn it into a strong puller
			else if (leasti>=0 && leastAbsTheta >= 0.25*Math.PI ) //there haven't been any pullers added, there's a candidate < 0.75PI
				{	leastsLinkx = han.links.length;
					han.addPlainLink(han.neighbors[leasti]).setRate(10).setRestLength(0); //add strong puller
				}
			if (leastsLinkx<0) alert("impossible leastsLinkx not defined in hanSolo smarts");
			//set dir in direction of puller that is closest to old value of dir
			dir = Math.atan2(han.links[leastsLinkx].target.y - han.y, han.links[leastsLinkx].target.x - han.x);

		}
		
		han.smarts = hanSoloSmarts;

		var steer = function(tlr) //tlr is -1 (hardest possible left) to +1 (hardest possible right)
		{	if (tlr< -1) tlr = -1;
			if (tlr>  1) tlr =  1;
			dir = stdAngle(dir+0.5*Math.PI*tlr);
		};

		var center = function(){return {x:han.x, y:han.y};}

		var direction = function(){return dir;}

		han.intrface = {steer:steer, center:center, direction:direction};

		return han;

	}

	//moving munchies
	function inchworm(){
		var head, tail, musclex,pcCycle,pcCount;

		head = pushRandomNode().setRadius(2.5).setGrip(10*gMinGrip);
		tail = pushRandomNode().setRadius(3).setGrip(1*gMinGrip);
		tail.stomach = head;
	  
		musclex = head.links.length;
		head.addLinkToBot(tail).setRestLength(9).setRate(1.4); //length half way through throw, not important
		// Will need gPhotoYield of over rate/numberofphotosynthesizers to maintain. 
		// Higher speed costs more--for higher rates need more.
		// Assuming gPhotoYield == 1, rate == number of photosynthesizers is about maintenance levels. If you want this to be
		// a food animal, rate should be lower so that it will accumulate oomph.

		//prep thread cycle, start at random place in cycle
		//w rate of 5, and throw (difference between short and long lengths) of 60, would take 12 steps if both were gMinGrip, 
		//and and about 24 (a bit less) steps if one is heavy and the other gMinGrip. So set pcCyle to 48 and gPhotoYield to 3
		//w rate of 1 would take 60 steps if both gmingrip, about 120 if one is heavy. So set pcCycle to 240. gPhotoYield could be about 0.5.
		//w rate of 2 would take 30 steps if both gmingrip, about 60 if one is heavy. So set pcCle to 120.

		//w rate of 1.4 and throw of 16-2 = 14, would take 10 steps if both were gMinGrip, about 20 if one is heavy. So
		pcCycle = 40; pcCount = Math.floor(Math.random()*pcCycle);

		//add smarts to bot head
		head.smarts = function(){

			if (pcCount == 0) {
				shiftGrip(1,[tail],[head]);
				this.tryRestLength(musclex, 16); //begin thrusting free stomach forward
			}

			if (pcCount == Math.round(pcCycle/2)) {
				shiftGrip(1,[head],[tail]);
				this.tryRestLength(musclex,2); //begin pulling anchored stomach back
			}

			pcCount = (pcCount+1)%pcCycle;

		};
		return head;
	}


	//A tapeworm is a meta-bot, an assemblage of bots. Each segment is its own stomach. 
	//If you cut it, the leftover parts will continue to function synchronously,
	//since they all share the same smartsData.

	//A tapeworm could be made to be all one bot, all sharing the same stomach, see below.

	function wormSegment(node, downCounter, smartsData) //smartsdata has original tailCount, pc and pcCycle
	{	node.smarts = function()
		{	//when called from thread tick, "this" will be node
			//different worms of same length, spawned at same time, are synchronized in phase
			// phase 1 when pc 0, decreases for the first half of the cycle to -1
			var phase = Math.cos(2*Math.PI*smartsData.pc/smartsData.pcCycle); 
			//The last segment, with downCounter zero, won't have any link
			if (downCounter > 0) this.tryRestLength(0,16+8*phase); //8 to 24. Most wiggle without shift in grip. 

			//firstNode is the only one running the pc, and shifting grip with its partner, to drag the tapeworm
			if (this == smartsData.firstNode) 
			{	if (smartsData.pc == 0 && this.links[0]) //maybe your link got eaten
					shiftGrip(1,[this],[this.links[0].target]); //while shrinking, heavy head pulls the worm
				if (smartsData.pc == smartsData.pcCycle/2 && this.links[0]) 
					shiftGrip(0,[this],[this.links[0].target]);//while expanding, push the light head
				smartsData.pc = (smartsData.pc + 1)%smartsData.pcCycle;  //all nodes in tapeworm share the same data
			}
	 	 }
	  
		if (downCounter>0) 
		{	var newNode = pushRandomNode().setRadius(4);//.setColor("yellow");
			node.addPlainLink(newNode).setRestLength(16).setRate(10); //use addLinkToBot to make them all the same bot
			wormSegment(newNode, downCounter-1, smartsData);
		}
	}

	function tapeworm(tailCount){ 

	  var node = pushRandomNode().setRadius(5).setGrip(1000*gMinGrip);//.setColor("chartreuse"); //node.stomach == node at birth
	 
	  //prep data packet shared by smarts function of all segments of the worm
	  var data = {};
	  data.pcCycle = 70; 
	  data.pc = 0; // start at 0 so that can predict phase of first, pulling, segment, to be growing in phase with grip transitions
	  data.tailCount = tailCount;
	  data.firstNode = node; //or clan of node

	  wormSegment(node,tailCount,data);
	  
		return node;
	}

	
	//Each node bears a different copy of smarts, rather than their sharing a prototype copy. 
	//But that's best, because that way can reassign them, can change their program.
	function tricycle()
	{	var head, tail1, tail2, muscle1x, muxcle2x, pc, pcCycle, tiller, goal;

		head = pushRandomNode().setRadius(7).makeEater().setGrip(gMinGrip*18).setClan("trike"); 
		tail1  = pushRandomNode().setRadius(5).setGrip(gMinGrip).setClan("trike");
		tail2 = pushRandomNode().setRadius(5).setGrip(gMinGrip).setClan("trike");
		//total 20xgMinGrip. Without excess grip shiftGrip can do nothing

		tail1.stomach = tail2.stomach = head;
	  
		// assuming gPhotoYield is 1, with three muscles to power off of two photosynthesizers, rate = 1/3 of 2 = 0.66
		// But you want this to be hungry, i.e. to be underpowered off its own two photosynthesizers. So go with a much higher rate
		muscle1x = head.links.length; //0
		head.addLinkToBot(tail1).setRestLength(14).setRate(1.33);

		muscle2x = head.links.length; //1
		head.addLinkToBot(tail2).setRestLength(14).setRate(1.33);

		tail1.addLinkToBot(tail2).setRestLength(14).setRate(1.33); //unchanging frame

		// at fully fed rate of 1.33, a muscle with throw of 40, would take 40/1.33 = 30 steps
		// if both ends gMingrip, twice that if one end is heavy, so say 60. 
		// On pull in, you have both links pulling a gMinGrip tail each, so should take 30 pull in.
		// Total is 90
		//prep program counter, start at random place in cycle. 
		pcCycle = 90; 
		pc = Math.floor(Math.random()*pcCycle);
		tiller = -0.1 + 0.2*Math.random(); //avg is 0. Barring steering, will go straight ahead, some have slight turning radius

		//can't be in prototype because needs closure access to variables tail1, tail2 etc
		//AND it's only on head, not on all nodes, and not on the bot, which isn't an object
		head.smarts = function()
		{	trySteerToGoal();

			//rotating the tail1-tail2 frame constitutes turning progress.
			//interpret tiller as turning -1 towards tail1 (muscle1 short slow, muscle2 long fast), 1 towards tail2 (vice versa).
			if (pc == 0) 
			{	shiftGrip(1,[tail1,tail2],[this]); //shift all possible grip to tails
				this.tryRestLength(muscle1x,40+4*tiller); //range 36 to 44
				this.tryRate(muscle1x,1.33+tiller/3); //range 1.0 to 1.66. Longer also faster, so even when underpowered will turn
				this.tryRestLength(muscle2x,40-4*tiller);
				this.tryRate(muscle2x,1.33-tiller/3);
			}

			if (pc == 60) //start pull back to restore equilateral
			{	shiftGrip(1,[this],[tail1,tail2]); //shift all possible grip head
				this.tryRestLength(muscle1x,14);  //restore equilateral, leaving rates as they were
				this.tryRestLength(muscle2x,14);
			}
/*
			if (pc == Math.round(pcCycle/3)) 
			{	shiftGrip(0,[this],[tail1,tail2]); //shift grip to tails
				this.tryRestLength(muscle1x,50); this.tryRestLength(muscle2x,50); //begin pushing head forward
			}

			if (pc == Math.round(2*pcCycle/3)) 
			{	shiftGrip(1,[this],[tail1,tail2]); //shift grip to head
				this.tryRestLength(muscle1x,14); this.tryRestLength(muscle2x,14); //begin pulling degripped tails toward head
			}
*/
			pc = (pc + 1)%pcCycle;

	   }

		var trySteerToGoal = function()
		{	if (!goal) return;
			var c = center(),
				theta = direction(),
				cPlus = { x: c.x+Math.sin(theta), y:c.y+Math.cos(theta) };
				offBy = signedAngle(goal,c,cPlus); // negative if we're going too far to the right, positive if too far to the left, botCentric
			if (offBy) steer(offBy/Math.PI);
		}

		//prepare user intrface
		// if goal is defined, 'manual' steering via the UI will be overridden by automatic attempt to go to goal
		var steer = function(tlr) //tlr is -1 (hardest possible left) to +1 (hardest possible right)
		{	if (tlr< -1) tlr = -1;
			if (tlr>  1) tlr =  1;
			if (signedAngle(tail1,head,tail2) > 0) //tail1 is left side of bot viewed from TAIL TOWARDS HEAD, tail2 is right side
				tiller = tlr;
			else tiller = -tlr; //tail2 is left side, tail1 is right side
			//console.log(" tlr: "+tlr.toFixed(3)+" tiller:"+tiller.toFixed(3));
		};

		var center = function(){return { x:(tail1.x+tail2.x)/2, y:(tail1.y+tail2.y)/2 };}

		var direction = function()
		{	if (signedAngle(tail1,head,tail2) > 0) //tail1 is left side of bot viewed from TAIL TOWARDS HEAD, tail2 is right side
				return signedAngle(tail2,tail1,{x:tail1.x, y:tail1.y + 20})-Math.PI/2;
			else
				return signedAngle(tail1,tail2,{x:tail2.x, y:tail2.y + 20})-Math.PI/2;
		}

		//This is a higher level steering capability than steer
		var setGoal = function(gol) //gol is a point that we're to steer towards. Use undefined to stop use of goal.
		{	goal = gol;
		}

		head.intrface = {steer:steer,  center:center, direction:direction, setGoal: setGoal }

		return head;
	}


	function spinner()
	{	var head,b,c, pc, pcCycle;

		head = pushRandomNode().setColor("LightPink").setRadius(4).setGrip(gMinGrip);
	  	b = pushRandomNode().setColor("LightCoral").setRadius(4).setGrip(10*gMinGrip);
		c = pushRandomNode().setColor("Orchid").setRadius(4).setGrip(10*gMinGrip);
		//put most of grip in b,c

		b.stomach = c.stomach = head.stomach = head;
	  
		head.smarts = function()
		{	if (pc == 0) 
			{	shiftGrip(1,[b],[c]);
				//breathe out
				head.tryRestLength(0,20); 
				b.tryRestLength(0,20);
				c.tryRestLength(0,20); 
			}

			if (pc == Math.round(pcCycle/2))
			{	shiftGrip(1,[c],[b]);
				//breathe in
				head.tryRestLength(0,8); 
				b.tryRestLength(0,8);
				c.tryRestLength(0,8); 
			}

			pc = (pc + 1)%pcCycle; 

		}

	  	head.addLinkToBot(b).setRestLength(10).setRate(2);
		b.addLinkToBot(c).setRestLength(10).setRate(2);
		c.addLinkToBot(head).setRestLength(10).setRate(2);

		//prep thread cycle, start at random place in cycle
		pcCycle = Math.floor(4+Math.random()*50); 
		pc = Math.floor(Math.random()*pcCycle);
	  
	}
	

	function rower()
	{	var head, tail, wing1, wing2, wing1Musclx, wing2Musclx, pcCycle, pc, imbalance;

		head = pushRandomNode().setRadius(4).setGrip(gMinGrip).setColor("aqua"); 
		tail = pushRandomNode().setRadius(4).setGrip(gMinGrip).setColor("black");
		wing1 = pushRandomNode().setRadius(12).setGrip(10*gMinGrip).setColor("blue"); 
		wing2 = pushRandomNode().setRadius(12).setGrip(10*gMinGrip).setColor("blue");

		wing2.stomach = wing1.stomach = tail.stomach = head.stomach = head;

		head.addLinkToBot(tail).setRestLength(20).setRate(2); //stiff backbone
		head.addLinkToBot(wing1).setRestLength(20).setRate(2); //stiff collarbone
		head.addLinkToBot(wing2).setRestLength(20).setRate(2); //stiff collarbone

		wing1Musclex = tail.links.length; //0
		tail.addLinkToBot(wing1).setRestLength(28).setRate(1);
	  	wing2Musclex = tail.links.length; //1
	  	tail.addLinkToBot(wing2).setRestLength(28).setRate(1);
	  
	  	//prep thread cycle, start at random place in cycle
		pcCycle = 97; pc = Math.floor(Math.random()*pcCycle);
		imbalance = extremalRandom(2);

		head.smarts = function()
		{	if (pc == 0) 
			{	//barring steering, occasional change of imbalance makes it wander
				if (Math.random()<0.1) imbalance = extremalRandom(2); //every 10th cycle or so

		 		shiftGrip(1,[head,tail],[wing1,wing2]); //shift grip from wings to head and tail
		 		//imbalance in wing grip doesn't do much here, since both wings have so little grip
				tail.tryRestLength(wing1Musclex,38); //swing wings forward
				tail.tryRestLength(wing2Musclex,38);
			}

			if (pc == Math.round(pcCycle/2))
			{	shiftGrip(1,[wing1,wing2],[head,tail]); //shift grip from head and tail to wings
				shiftGrip(imbalance,[wing1],[wing2]);// use imbalance to turn
		 
				tail.tryRestLength(wing1Musclex,6); //pull wings back
				tail.tryRestLength(wing2Musclex,6);
			}

			pc++; if (!(pc%pcCycle)) { pc = 0; }
		}
	}


/////////////////////////////////////////// installing AI's

	/* An experiment with what "this" is:
	n = { a:"flum", b: 18 }
	//Object {a: "flum", b: 18}
	function Nip(){ this.a = "film";}
	//undefined
	Nip.prototype.smarts = function(){ console.log(this.a); }
	//Nip.smarts()
	nipper = new Nip();
	//Nip {a: "film"}
	n.smarts = nipper.smarts   //but they're nooooot. Because they belong to, and access, different objects: their "this" is different
	//Nip.smarts()
	n.smarts()
	// flum
	nipper.smarts()
	// film
	function Nife(){ this.a = "film"; this.smarts = function(){ console.log(this.a) };} //even if it's defined inline, within the textual context
	//undefined
	nifer = new Nife()
	//Nife {a: "film"}
	nifer.smarts()
	// film
	n.smarts = nifer.smarts
	//Nife.smarts()
	n.smarts()
	// flum

	// now take this to the next level, note the Nike smarts function accesses parameter w, and variable avar, within its closure
	function Nike(w){ this.a = "film"; var avar = w; this.diff = "something"; 
	this.smarts = function(){ console.log(this.a); console.log(avar); console.log(w); console.log(this.diff); };}
	//undefined
	shoe = new Nike("golly");
	//Nike {a: "film", diff: "something"}
	shoe.smarts()
	// film
	// golly
	// golly
	// something
	//undefined
	n.smarts = shoe.smarts
	//Nike.smarts()
	n.smarts()
	// flum
	// golly
	// golly
	// undefined

	// the var's in a closure are in object instances, not tied to the single function definition
	function foo(x){ var wow = 19; x.smarts = function(){ wow++; console.log(wow);}}
	//undefined
	a = { pal: 4}
	//Object {pal: 4}
	b = { nok: 5 }
	//Object {nok: 5}
	foo(a);
	//undefined
	a.smarts()
	// 20
	// undefined
	a.smarts()
	// 21
	// undefined
	foo(b)
	// undefined
	b.smarts()
	// 20
	//undefined
	a.smarts()
	// 22
	// undefined
	*/



	// AI: a function which changes the given node's programming in response to threats from other bots.
	// call installFirstAI on a given node to install the AI's smarts function as the node's smarts function.

	// Need to limit code's ability to call linkAction.. . will make it private in Node? How can it be attached to a node within
	// Node module, but not
	// How to make it public to gNodes class, but not to world...
	// else it could do multiple linkActions in one smarts. This could be done by a check in linkAction against gTicCount

	function installFirstAI(node)
	{	var state = "hunter", //is normal state
			fightingLinkIndex, opponent, opponentClan, 
			oldSmarts = node.smarts;

			startRepeller = function(attacker)
			{	opponent = attacker; opponentClan = opponent.clan;
				fightingLinkIndex = node.links.length;
				node.addPlainLink(attacker).setRate(100).setRestLength(this.distance(attacker)*9.999); //add fighting repeller link
				state = "repeller";
			},

			startTractor = function(target)
			{	opponent = target; opponentClan = opponent.clan;
				fightingLinkIndex = node.links.length;
				node.addPlainLink(target).setRate(100).setRestLength(0); //add fighting tractor link
				state = "tractor";
			},

			stopFighting = function()
			{	node.breakLinkR(fightingLinkIndex); //delete fighting link
				opponent = undefined;
				state = "hunter";
			},

			smarts = function() // by the time smarts executes, "this" will be a node
			{	switch (state)
				{ 
					case "hunter":
						var besti = -1, bestOomph = 0, oops = false, them;
						for (var i=0;i<this.neighbors.length;i++)
						{	them = this.neighbors[i];
							if (them.clan != this.clan)
							{	if (them.isEater() && them.stomach.oomph > this.stomach.oomph) { oops = true; break; }
								if (them.oomph > bestOomph) {bestOomph = them.oomph; besti = i;}
							}
						}
						if (oops) startRepeller(them);
						else if (bestOomph > 4.56) //it will cost something to tractor them in
							startTractor(this.neighbors[besti]); //having multiple tractors not a good idea, they compete with each other
						break; //keep hunting

					case "repeller": 
						//until he has eaten me (in which case this.smarts will be set to undefined, can't get to here), or
						//his oomph gets drawn down below mine (I should change tactics, or maybe I ate him somehow) or
						//I can't see him so I think he can't see me, AND his tractor link to me, if any, is broken.
						if (opponent.stomach.oomph < this.stomach.oomph) { stopFighting(); break; }
						if (this.neighbors.indexOf(opponent) < 0)
						{	var hisLinkToMex = opponent.links.indexOf(this);
							//stopFighting if he has no link to me, or if he's repelling me
							if (hisLinkToMex < 0 || opponent.links[hisLinkToMex].restLength > this.distance(opponent)) 
							{ stopFighting(); break; }
						}
						break; //keep repelling

					case "tractor":
						//until he has eaten me (in which case this.smarts will be set to undefined, can't get to here), or
						//his available oomph becomes larger than mine (scary, I should change tactics)
						//or I (or someone else) have eaten him (so he failed and was dismembered and naiveStated so his clan has changed)
						if (opponent.stomach.oomph > this.stomach.oomph || opponent.clan != opponentClan )
						{	stopFighting(); break; }
						break; //keep tractoring
					              
				}; //end switch

				if (oldSmarts) oldSmarts(); //could have been undefined

			}; //end smarts function and end of var declarations

		return smarts; //smarts is the only public access

	}

return { tricycle:tricycle, inchworm:inchworm, hanSolo:hanSolo, spinner:spinner, tapeworm:tapeworm, rower:rower, installFirstAI:installFirstAI };

}

/*
TO DO: use Canvas instead of SVG
fix firstAI to be compatible with new EAT option that sucks all oomph but does not naiveState the eaten,
(current "tractor" state will never terminate). 
If all steering included some hanSolo element, could make all bots turn once they've left all others far behind, just like hansolo does now, i.e. wouldn't need gravity
Make inchworm steerable like hanSolo, by tractating aliens just to steer.
make AI installable on better steering bots--make ALL of them support in interface
a folded rower is a perfect rotator--use that same technique in tricycle for steering.

*/
