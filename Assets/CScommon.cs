using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

// must be maintained in common between client and server
public static class CScommon {

	public static int serverPort = 8888;

	public const short nodeIdMsgType = 300; //intMsg, server tells a client which bubble they are associated with, often in response to their requestNodeIdMsg
	public const short updateMsgType = 301; //UpdateMsg, server sends to all clients frequently to update positions of bubbles. Uses DynamicNodeData[]
	public const short scoreMsgType = 302; //server sends player scores to client
	public const short targetNodeType = 303; //TargetNodeMsg, client tells server that they want a link made from their bubble to a given target bubble
	//public const short lookAtNodeType = 304; //unused
	public const short initMsgType = 305; // InitMsg, server sends to clients relatively static info on many bubbles.
	public const short initRequestType = 306; //stringMsg containing a name. Client affirms having received gameSizeMsg, requests initialization
	public const short push1Pull2MsgType = 307; //intMsg, manual push/pull 1:push, 2:Pull, 3. togglePushPull, 0. return to automatic pushPull
	//public const short keyMsgType = 308; // unused, could send client keypresses to server
	public const short initRevisionMsgType = 309; //InitRevisionMsg, server sends to all clients infrequently, to update relatively static initMsg data.
	public const short requestNodeIdMsgType = 310; //intMsg, client requests being associated with a given bubble. Server responds with nodeIDMsgType.
	public const short gameSizeMsgType = 311; //GameSizeMsg with numNodes, numLinks, worldRadius
	//public const short nameNodeIdMsgType = 312; //NameNodeIdMsg, tells all clients what user name is associated with what bubble
	public const short turnMsgType = 313; //intMsg, -1 means change direction a bit to the left, +1 means a bit to the right, 0 indicates go straight.
	//public const short forward0Reverse1Type = 314; //intMsg, 0 means forward, 1 means reverse. 2 means toggle. Changes speed to -speed.
	public const short linksMsgType = 315; //linksMsg
	public const short restartMsgType = 316; //intMsg, sent from client to restart server game.
	// value 0 is the exception, it pauses/unpauses current game without restarting it.
	// value -1 relaunches the current game, without changing current scale values.
	// Values 1-9 select a particular predefined game setup to be launched with its default scale values.
	// Other values scale and restart the current game:
	// 21/22 scale down/up the average size (radius) of nodes, 
	// 31/32 scale down/up the ratio between the size of hunter organisms and the size of other organisms,
	// 41/42 scale down/up photoYield, the rate energy trickles into everyone's tanks, i.e. the 'starved' speed of everything
	// 51/52 scale down/up baseMetabolicRate, the base rate at which muscles consume energy, i.e. the 'fed' speed of everything
	// 61/62 scale down/up the worldRadius (which scales up/down the relative lengths of links in the world, i.e. the size of organisms )
	// 71/72 move down/up by 1/10 between 0 and 1 the fraction of their maxOomph fed to veg nodes before launch
	// 81/82 move down/up by 1/10 between 0 and 1 the fraction of their maxOomph fed to nonveg nodes before launch

	public const short speedMsgType = 317; //intMsg, sent from client to change speed of its muscles demand. Range from -300 to 300, negative values reverse gear.
	public const short broadCastMsgType = 318; //stringMsg, sent from client to server, and rebroadcast by server to all clients.
	public const short scaleMsgType = 319; //stringMsg, sent from server to all clients whenever scales are set or changed, a very succinct summary of scales
	public const short nodeNamesMsgType = 320;

	//use value of zero to toggle server "pause" state without changing game.

//  Summary of messagery:
	
//  Client starts with a nodeID of -1.
//  Client sends myNetworkClient.Connect(serverIP, CScommon.serverPort)
//  Server handles MsgType.Connect, replies with gameSizeMsg with numNodes, numLinks and worldRadius
//	Client responds with initRequestMsg including a name
//	Server replies with a suite of reliable initMsgs and updateMsgs and linkMsgs that cover the whole world.

//  If user clicks on a desired node to mount, client sends a requestNodeId for that node.
//	The server replies with a nodeIdMsg which either contains their old, unchanged bubble assignment
//  (which may have been -1) which indicates their request is denied for some reason, OR it contains an assigned nodeId 
//	(which may be different from the one requested).
//  Server also sends nameNodeIdmsg to all clients associating the name to the node.
//  If request granted, server will issue an initRevisionMsg of the changed DNA (reflecting that the node is mounted) to all connected clients.

//  When game is unpaused, server sends regular unreliable updateMsgs,
//  and irregularly sends reliable initRevisionMsgs and linkMsgs.

//  If server starts a new game, it issues a new gameSizeMsg to all connected clients.

	//make sure bone is not first one, it must not be default value since bones can't be changed to another type.
	public enum LinkType : byte {pusher, puller, bone} 

	public class floatMsg: MessageBase {
		public float value;
	}

	public class intMsg: MessageBase {
		public int value;
	}

	public class stringMsg: MessageBase {
		public string value;
	}

	public class KeyMsg: MessageBase {
		public KeyCode keyCode;
	}

	public class GameSizeMsg: MessageBase {
		public int numNodes;
		public int numLinks;
		public float worldRadius;
	}

	public class NameNodeIdMsg : MessageBase{
		public string name;
		public int nodeIndex;
	}


	public struct NodeName{
		public int nodeId;
		public string name;
	}

	public class NodeNamesMsg: MessageBase{
		public NodeName[] arry;
	}

	public struct ScoreStruct{
		public int nodeId;
		public int plus;
		public int minus;
	}

	public class ScoreMsg: MessageBase{
		//true if this message sends two ScoreStructs, arry[0] being the winner, arry[1] being the loser.
		public bool zeroAteOne; //false if this message is a list of initial scores (like when you first join an ongoing game).

		public ScoreStruct[] arry;
	}

//	public class LinkTypeMsg : MessageBase {
//		public LinkType linkType;
//	}
	

	public class TargetNodeMsg : MessageBase{
		public int nodeIndex;
		public LinkType linkType;
		public byte hand;
	}
	
	public struct StaticNodeData {
		public float radius;
		public long dna;
	}

	public struct DynamicNodeData {
		public Vector2 position;
		public float oomph;
	}
	
	//for (i = start; i<start+nodeData.Count; i++) of an array of nodes.
	public class InitMsg: MessageBase{
		public int start;
		public StaticNodeData[] nodeData;
	}

	//for (i = start; i<start+nodeData.Count; i++) of an array of nodes.
	public class UpdateMsg: MessageBase{
		public int start;
		public DynamicNodeData[] nodeData;
	}

	public struct StaticNodeInfo {
		public int nodeIndex;
		public StaticNodeData staticNodeData;
	}
	
	public class InitRevisionMsg: MessageBase{
		public StaticNodeInfo[] nodeInfo;
	}

	public struct LinkData {
		public bool enabled; //if this is false, disregard this link
		public int sourceId; // an index into nodes
		public int targetId; // an index into nodes
		public LinkType linkType;
	}

	public struct LinkInfo {
		public int linkId; // an int in 0..linkCount-1
		public LinkData linkData;
	}

	public class LinksMsg: MessageBase{
		public LinkInfo[] links;
	}


	public static readonly float maxOomphFactor = 200;
	public static readonly float inefficientLink = 60; // distance/radius at which link efficiency first term drops to 1/e ~ 0.36788
	public static readonly float inefficientLink2 = inefficientLink*inefficientLink;

	public static float maxOomph(float radius){ return maxOomphFactor*radius*radius;}

	private static float distance2(Vector2 source, Vector2 target){
		return (target.x-source.x)*(target.x-source.x) + (target.y-source.y)*(target.y-source.y);
	}

	// Returns a number between 0 and 1, the efficiency of a relationship between two nodes.
	// Efficiency is a function of the distance between the two nodes AND the source's radius--so efficiency is NOT symmetric.
	public static float efficiency(float distanceSquared, float sourceRadiusSquared){
		float d2 = distanceSquared/sourceRadiusSquared; // 1 at distance == radius, 100 at distance == 10 radius
		d2 /= inefficientLink2; // 1 at distance/radius == inefficientLink
		return Mathf.Max (0.00001f,Mathf.Exp (-d2)); // 1/e ~ 0.36788 at distance == inefficientLink
		//avoid zerodivide in muscleAction by never having efficiency of zero.
		//penalizes longer distance, and is scaled by the radius of the node. Bigger nodes can more efficiently
		//run large muscles, though their metabolic rate may be no faster. 
		//So they're "proportionately" (to their radius) slower but more efficient,
		//though absolutely the same speed but more efficient.
	}

	//demand is the oomph consumed by a muscle every fixedframe.
	//(with one exception: if the muscle isPuller(), and the pulled node's center is already within the source's radius, it draws no power and does nothing)
	//link strength = demand * efficiency(linkLengthSquared,sourceRadiusSquared) is the oomph actually delivered into movement by the enabled muscle, as degraded by the efficiency of the muscle.
	//The units of both are units of oomph.
	//i.e., disabled muscles (demand == 0) don't do anything and don't draw any oomph.
	//Note that a node running n equally enabled muscles is consuming its oomph n times faster than a node running just 1 such muscle, 

	//If we don't send the client the full info on "demand", but rather only the boolean "enabled" which is demand>0, the compromised display
	//could be to render links with width proportional to 
	//		strength = enabled? someFactor * sourceRadiusSquared * efficiency(linkLengthSquared,sourceRadiusSquared) : 0;

	//Bone link strength is a constant, whatever displays well. Bones are never disabled, and do not change with distance or oomph 


	//dna

	public const int vegetableBit = 0; // if false isEater, if true !isEater. We should rename this "predatorBit" and flip all our uses of it.
	public const int noPhotoBit = 1;  // higher link rate, with accompanying power demand and inefficiency
	public const int playerBit = 3;   //indicates that this node has been allocated to a player (even though the player may be offline).
	public const int playerPlayingBit = 4;  //indicates that this node has been allocated to a player, and that this player is currently online and playing.
	public const int snarkBit = 5;
	public const int strengthBit = 6;

	//thanks to http://www.dotnetperls.com/and
	public static string longToString(long dna)
	{	// Displays bits.
		char[] b = new char[64];
		int pos = 63;
		int i = 0;
		
		while (i < 64)
		{
			if ((dna & (1L << i)) != 0L) b[pos] = '1';
			else b[pos] = '0';

			pos--;
			i++;
		}
		return new string(b);
	}
	
	//bitNumber 0 is least significant bit
	public static bool testBit(long dna, int bit)
	{ return ((dna & (1L << bit)) != 0L);}

}
