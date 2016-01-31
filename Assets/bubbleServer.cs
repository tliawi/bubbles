
using UnityEngine;
using System; // for GC
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
//using UnityEngine.Networking.Types; //match
//using UnityEngine.Networking.Match;
using UnityEngine.UI;

public class bubbleServer : MonoBehaviour {
	
	private static List<string> debugDisplayList = new List<string>();

	public static void debugDisplay(string s){
		if (debugDisplayList.Count>8) debugDisplayList.RemoveAt(0);
		debugDisplayList.Add (s);

		string ss = "";
		for (int i=0;i<debugDisplayList.Count; i++) ss += debugDisplayList[i] + "\n";

		if (dbgdsply.activeSelf) {
			GameObject.Find ("Canvas/Scroll View/Viewport/myScrollContent").GetComponent<Text>().text = ss;
		}
		Debug.Log(s);
	}

	private static bool displayGrid;

	//Bubbles will spawn with radii approximately 1 (i.e. from let's say 1/10 to 10).

	//private bool xing = false;

	private static bool paused = true;
	public static bool constantLinkWidth = false;

	private static float normScale;
	private static float abnormScale;
	public static float norm {get {return normScale;}}
	public static float abnorm {get{ return normScale*abnormScale;} }
	private static int normScaleI, abnormScaleI, photoYieldI, baseMetabolicRateI, worldRadiusI;
	private static float vegStartFuel, nonvegStartFuel;
	public static int popcorn {get; private set;} 

	private static Dictionary<int,bool> scheduledScores = new Dictionary<int,bool>();

	private void resetDefaultScales(int newGame){
		switch (newGame) {
		case 1: //snark
			normScaleI = 9;
			abnormScaleI = 2;
			photoYieldI = 0;
			baseMetabolicRateI = 0;
			worldRadiusI = 0;
			vegStartFuel = 1.0f;
			nonvegStartFuel = 0f;
			popcorn = 100;
			break;
		case 2: // race
			normScaleI = 2;
			abnormScaleI = 6;
			photoYieldI = -3;
			baseMetabolicRateI = -1;
			worldRadiusI = 0;
			vegStartFuel = 1.0f;
			nonvegStartFuel = 0f;
			popcorn = 100;
			break;
		case 3: //fussball
			normScaleI = 5;
			abnormScaleI = 5;
			photoYieldI = 0;
			baseMetabolicRateI = 0;
			worldRadiusI = -5;
			vegStartFuel = 1.0f;
			nonvegStartFuel = 0f;
			popcorn = 100;
			break;
		case 4: //turm
			normScaleI = -1; 
			abnormScaleI = 4;
			photoYieldI = 0;
			baseMetabolicRateI = 0;
			worldRadiusI = 0;
			vegStartFuel = 6f;
			nonvegStartFuel = 0f;
			popcorn = 250;
			break;
		default:
			normScaleI = 6;
			abnormScaleI = 1;
			photoYieldI = 0;
			baseMetabolicRateI = 0;
			worldRadiusI = 0;
			vegStartFuel = 1.0f;
			nonvegStartFuel = 0f;
			popcorn = 100;
			break;
		}
		setScales();
		quitGame(newGame);
	}

	private static string scaleString(){
		return "| "+normScaleI+" "+abnormScaleI+"   "+
			photoYieldI+" "+baseMetabolicRateI+" "+worldRadiusI+"   "+
			Mathf.Round(vegStartFuel*10)+" "+Mathf.Round (nonvegStartFuel*10)+"  "+popcorn;
	}

	private void setScales(){
		normScale = Mathf.Pow(1.2f, normScaleI);
		abnormScale = Mathf.Pow(1.2f, abnormScaleI);
		Bub.photoYield =0.08f* Mathf.Pow(1.2f, photoYieldI);
		Bub.baseMetabolicRate = 0.0035f * Mathf.Pow (1.2f, baseMetabolicRateI);
		Bub.worldRadius = 400f * Mathf.Pow(1.2f, worldRadiusI);
		if (vegStartFuel < 0) vegStartFuel = 0;
		if (vegStartFuel > 1) vegStartFuel = 1;
		if (nonvegStartFuel < 0) nonvegStartFuel = 0;
		if (nonvegStartFuel > 1) nonvegStartFuel = 1;
		if (popcorn < 0) popcorn = 0;
		sendScalesToAll();
	}
	
	private int currentGame = 1;

	private CScommon.InitMsg referenceInitMsg;
	private CScommon.LinksMsg referenceLinkMsg;

	public struct JKStruct { 
		public int j; 
		public int k;
		public JKStruct(int j0, int k0) {
			j = j0; k = k0;
		}
	}

	private JKStruct[] referenceLinkJK;

	private int oldTickCounter;
	private Text reminderText;
	private static System.Diagnostics.Stopwatch gameStopwatch = new System.Diagnostics.Stopwatch();

	public class PlayerInfo {
		public int connectionId;
		public string name;
		public CScommon.ScoreStruct data;

		public PlayerInfo(){
			connectionId = -1;
			name = "";
			data.nodeId = -1;
		}

		public void clearScore(){ data.plus = 0; data.minus=0; data.performance = 0; data.neither0Winner1Loser2 = 0;
			data.gameMilliseconds = gameStopwatch.ElapsedMilliseconds; }
	}

	private static Dictionary<int, PlayerInfo> connectionIdPlayerInfo = new Dictionary<int, PlayerInfo>();
	// replaces private Dictionary<int,int> connectionIdNodeId = new Dictionary<int,int>();
	// and private Dictionary<int,string> connectionIdName = new Dictionary<int,string>();
	
	//NOTE: some players (NPC's like snarks) may be in nodeIdPlayerInfo but they are not "connected" and so are not in connectionIdPlayerInfo.
	//Some players (connected spectators) may be in connectionIdPlayerInfo but they are not associated with any node, so are not in nodeIdPlayerInfo.
	private static Dictionary<int, PlayerInfo> nodeIdPlayerInfo = new Dictionary<int,PlayerInfo>();

	public static bool registered(int nodeId) {return nodeIdPlayerInfo.ContainsKey (nodeId);}



//	public static void playerWinLose(int winnerId, int loserId){
//
//		if (nodeIdPlayerInfo.ContainsKey (winnerId) && nodeIdPlayerInfo.ContainsKey(loserId)) { //don't track interactions between non-registrants
//
//			nodeIdPlayerInfo[winnerId].scorePlus += 1;
//			nodeIdPlayerInfo[loserId].scoreMinus += 1;
//
//			send2ScoresToAll(winnerId, loserId);
//
//		}
//	}

	static Dictionary<int,System.Diagnostics.Stopwatch> stopwatches = new Dictionary<int,System.Diagnostics.Stopwatch>();
	//holds one stopwatch for every nodeId for which you want to display performance.
	//Whenever you get a scoreMsg for a nodeId, you .Reset() and .Start() its stopwatch.
	public static float currentPerformance(CScommon.ScoreStruct ss){
		long delta = stopwatches[ss.nodeId].ElapsedMilliseconds; //the amount of time, in milliseconds, since you last received a scoreMsg for this player
		return ss.performance*Mathf.Pow(2,-delta/CScommon.performanceHalfLifeMilliseconds);
	}

	public static void score(int nodeId, byte neither0Winner1Loser2){
		if (nodeIdPlayerInfo.ContainsKey(nodeId)){
			if (neither0Winner1Loser2==1) nodeIdPlayerInfo[nodeId].data.plus += 1;
			if (neither0Winner1Loser2==2) nodeIdPlayerInfo[nodeId].data.minus += 1;
			nodeIdPlayerInfo[nodeId].data.neither0Winner1Loser2 = neither0Winner1Loser2;

			int change = neither0Winner1Loser2==0?0:neither0Winner1Loser2==1?1:-1;
			long nowMs = gameStopwatch.ElapsedMilliseconds;
			long deltaMs = nowMs - nodeIdPlayerInfo[nodeId].data.gameMilliseconds;
			nodeIdPlayerInfo[nodeId].data.performance *= Mathf.Pow(2,-deltaMs/CScommon.performanceHalfLifeMilliseconds);
			nodeIdPlayerInfo[nodeId].data.performance += change;
			nodeIdPlayerInfo[nodeId].data.gameMilliseconds = nowMs;

			scheduledScores[nodeId] = true;
		}
	}

	public static void scoreWinner(int nodeId){ score(nodeId,1);}
	public static void scoreLoser(int nodeId){ score(nodeId,2);}
		

	public static void registerNPC(int nodeId,string name){
		nodeIdPlayerInfo[nodeId] = new PlayerInfo();
		nodeIdPlayerInfo[nodeId].data.nodeId = nodeId;
		nodeIdPlayerInfo[nodeId].name = name;
	}


	private static GameObject dbgdsply;
	
	private static float zoom = 1;
	private static float zoomSpeed = 1.05f;

	void zoomCameraIn() {
		zoom /= zoomSpeed;
		Camera.main.orthographicSize /= zoomSpeed;
	}
	
	void zoomCameraOut() {
		zoom *= zoomSpeed;
		Camera.main.orthographicSize *= zoomSpeed;
	}
	
	void stepCamera( float x, float y) {
		Vector3 temp = Camera.main.transform.position;
		temp.x += x*zoomSpeed;
		temp.y += y*zoomSpeed;
		Camera.main.transform.position = temp;
	}

//	List<MatchDesc> matchList = new List<MatchDesc>();
//	NetworkMatch networkMatch;

	void Awake () { 

		QualitySettings.vSyncCount = 0;  // VSync must be disabled
		Application.targetFrameRate = 50;

		Grid.initialize();
		reminderText = GameObject.FindWithTag ("betweenGames").GetComponent<Text>();
		dbgdsply = GameObject.FindWithTag("scrollView");
		displayGrid = true;

//		networkMatch = gameObject.AddComponent<NetworkMatch>();
//
//		CreateMatchRequest create = new CreateMatchRequest();
//		create.name = "bub";
//		create.size = 32;
//		create.advertise = true;
//		create.password = "";
//		networkMatch.CreateMatch(create, OnMatchCreate);

		resetDefaultScales(1);

		//masterserver
//		bool useNat = !Network.HavePublicAddress();
//		Network.InitializeServer(32, CScommon.serverPort, useNat);
//		MasterServer.RegisterHost("Bubbles", "debugGame", "debug server");
	}
	

	void Start(){
		SetupServer();
	}

	void quitGame(int newgame){
		
		paused = true;
		gameStopwatch.Reset();
		dbgdsply.SetActive(true);

		referenceInitMsg = null;
		referenceLinkMsg = null;
		referenceLinkJK = new JKStruct[0];

		scheduledScores.Clear();

		Grid.deallocate ();
		Engine.deallocate(); //kills all nodes, with their rules, with their muscles, so whole bots setup is destroyed

		//the one thing I DON'T do is disconnect everyone, they can stay connected for next game.
		//Their connectionId and name are still valid. Their node assignments are off.
		foreach (int cId in connectionIdPlayerInfo.Keys) { 
			connectionIdPlayerInfo[cId].data.nodeId = -1; //disassociate from nodeId
		}

		//remove NPC and PC registrations
		nodeIdPlayerInfo.Clear();

		GC.Collect(); //while I'm at it...

		currentGame = newgame;
		initCurrentGame();
	}

	void initCurrentGame()
	{	
		paused = true;
		scheduledScores.Clear();

		Bub.initialize();
		Bots.initialize(currentGame);
		startFuel();
		Grid.initialize ();
		Grid.display(); //since start paused, want to be able to see the paused initial game state.
		Engine.initialize();

		generateReferences();

		foreach (var connectionId in connectionIdPlayerInfo.Keys) sendGameSize(connectionId); //and so do any clients who are still connected

		Camera.main.orthographicSize = Bub.worldRadius/2;
		Camera.main.transform.position = new Vector3(0,0,-100);

	}

	/*
		float inf = Mathf.Infinity, len = 12.0f;
		float a = inf - inf;
		float b = inf - len;
		float c = inf/inf;
		float d = (inf - len)/Mathf.Max (inf, len);
		if (Debug.isDebugBuild) debugDisplay ("inf - inf:"+a+" inf-12:"+b+" inf/inf:"+c+" (inf-12)/Max(inf, 12):"+d);
		//output:inf - inf:NaN inf-12:Infinity inf/inf:NaN (inf-12)/Max(inf, 12):NaN
		*/

	void FixedUpdate(){ if (!paused) Engine.step();}

	public static string gameName = "";

	string reminder(){
		string s = gameName+": arrows Zz s d g 1 2 3 4 +- 0";
		foreach (var v in connectionIdPlayerInfo) s += " "+v.Key+":"+v.Value.data.nodeId;
		s += "  "+(paused?"(PAUSED)":"")+scaleString();
		return s;
	}

	void togglePause(){
		paused = !paused;
		if (paused) gameStopwatch.Stop();
		else gameStopwatch.Start(); //starts from wherever it left off
	}
		
		void Update(){

//		if (gameChosen>0){

		reminderText.text = reminder();


		//		if (Input.GetKeyDown (KeyCode.X)) { 
//			xing = !xing; 
//		}

		if (Input.GetKeyDown(KeyCode.T)){ //testing sandbox
			long lng;
			if (Debug.isDebugBuild) debugDisplay(CScommon.longToString(Bub.setBit(1023L,0,0,1)));
			if (Debug.isDebugBuild) debugDisplay(CScommon.longToString(Bub.setBit(1023L,0,0,0)));
			lng = Bub.setBit(1023L,3,2,0);
			if (Debug.isDebugBuild) debugDisplay(CScommon.longToString(lng) + " " + CScommon.dnaNumber(lng,3,2));
			lng = Bub.setBit(1023L,3,2,1);
			if (Debug.isDebugBuild) debugDisplay(CScommon.longToString(lng) + " " + CScommon.dnaNumber(lng,3,2));
			lng = Bub.setBit(1023L,3,2,2);
			if (Debug.isDebugBuild) debugDisplay(CScommon.longToString(lng) + " " + CScommon.dnaNumber(lng,3,2));
			lng = Bub.setBit(1023L,3,2,3);
			if (Debug.isDebugBuild) debugDisplay(CScommon.longToString(lng) + " " + CScommon.dnaNumber(lng,3,2));

			lng = Bub.setBit(1023L,14,4,255*2);
			if (Debug.isDebugBuild) debugDisplay(CScommon.longToString(lng) + " " + CScommon.dnaNumber(lng,14,4));

		}

		if (Input.GetKeyDown (KeyCode.S) && paused){ //step
			togglePause();
			Engine.step ();
			execute();
			paused = true;
		}

		if (Input.GetKeyDown(KeyCode.L)){
			constantLinkWidth = !constantLinkWidth;
		}
			
		if (Input.GetKeyDown (KeyCode.G)) { 
			displayGrid = !displayGrid; 
		} 

		if (Input.GetKeyDown (KeyCode.D)){
			dbgdsply.SetActive (!dbgdsply.activeSelf);
			if (dbgdsply.activeSelf) if (Debug.isDebugBuild) debugDisplay(""); //to render annotations made while it was inactive
		}

		if (Input.GetKey(KeyCode.Z)){
			if (Input.GetKey(KeyCode.LeftShift)) zoomCameraIn ();
			else zoomCameraOut ();
		}
		if (Input.GetKey(KeyCode.UpArrow)) stepCamera(0,1);
		if (Input.GetKey(KeyCode.DownArrow)) stepCamera(0,-1);
		if (Input.GetKey(KeyCode.LeftArrow)) stepCamera(-1,0);
		if (Input.GetKey(KeyCode.RightArrow)) stepCamera(1,0);


		if (Input.GetKeyDown (KeyCode.Alpha0)) restartGame(0);
		
		for (int i = 1; i<10; i++){
			if (Input.GetKeyDown(""+i)) { restartGame(i); }
		}

		if (Input.GetKeyDown(KeyCode.BackQuote)) restartGame(-1);

		if (Input.GetKeyDown(KeyCode.Minus)) { 
			if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) restartGame(22);
			else restartGame(21);
		}
		if (Input.GetKeyDown(KeyCode.Equals)) {
			if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) restartGame(32);
			else restartGame(31);
		}
		if (Input.GetKeyDown(KeyCode.LeftBracket)) { 
			if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) restartGame(42);
			else restartGame(41);
		}
		if (Input.GetKeyDown(KeyCode.RightBracket)) {
			if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) restartGame(52);
			else restartGame(51);
		}
		if (Input.GetKeyDown(KeyCode.Backslash)) {
			if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) restartGame(62);
			else restartGame(61);
		}
		if (Input.GetKeyDown(KeyCode.Semicolon)) {
			if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) restartGame(72);
			else restartGame(71);
		}
		if (Input.GetKeyDown(KeyCode.Quote)) {
			if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) restartGame(82);
			else restartGame(81);
		}
		if (Input.GetKeyDown(KeyCode.Slash)) {
			if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) restartGame(92);
			else restartGame(91);
		}
		execute();
	}

	void execute(){
		if (!paused){
			if (displayGrid) {
				Grid.display();
			}
			
			//might be multiple updates per FixedUpdate. Don't bother to send changes if there are none.
			//For wisdom on unity event sequence, see http://docs.unity3d.com/Manual/ExecutionOrder.html
			//and discussion at http://forum.unity3d.com/threads/the-truth-about-fixedupdate.231637/
			if (oldTickCounter != Engine.tickCounter) {
				oldTickCounter = Engine.tickCounter;
				sendUpdateMsg();
			}
		}
	}

	public static void startFuel(){
		for (int i = 1; i< Engine.nodes.Count; i++) {
			if (CScommon.testBit (Engine.nodes[i].dna,CScommon.vegetableBit)) Engine.nodes[i].oomph = Engine.nodes[i].maxOomph*vegStartFuel; 
			else Engine.nodes[i].oomph = Engine.nodes[i].maxOomph*nonvegStartFuel;
		}
	}
		

//	public bool matchCreated;
//
//	public void OnMatchCreate(CreateMatchResponse matchResponse)
//	{
//		if (matchResponse.success)
//		{
//			if (Debug.isDebugBuild) debugDisplay("Create match succeeded");
//			matchCreated = true;
//			Utility.SetAccessTokenForNetwork(matchResponse.networkId, new NetworkAccessToken(matchResponse.accessTokenString));
//			/////////////NetworkServer.Listen(new MatchInfo(matchResponse), 9000);
//		}
//		else
//		{
//			Debug.LogError ("Create match failed");
//		}
//	}

	//byte reliableChannel, unreliableChannel, bigMsgChannel;

	// Create a server and listen on a port
	void SetupServer()
	{

		NetworkServer.Listen(CScommon.serverPort);

		//if (Debug.isDebugBuild) debugDisplay("Registering server callbacks");
		NetworkServer.RegisterHandler (MsgType.Connect, OnConnectedS);
		NetworkServer.RegisterHandler (MsgType.Disconnect, OnDisconnectedS);
		NetworkServer.RegisterHandler (CScommon.requestNodeIdMsgType, onRequestNodeId);

		//NetworkServer.RegisterHandler (CScommon.vegFlipType, onVegFlip); //unused
		NetworkServer.RegisterHandler (CScommon.targetNodeType, onTargetNode);
		//NetworkServer.RegisterHandler (CScommon.lookAtNodeType, onLookAtNode);//unused
		NetworkServer.RegisterHandler (CScommon.initRequestType, onInitRequest);
		//NetworkServer.RegisterHandler (CScommon.push1Pull2MsgType, onPush1Pull2Msg);
		NetworkServer.RegisterHandler (CScommon.turnMsgType, onTurnMsg);
		//NetworkServer.RegisterHandler (CScommon.forward0Reverse1Type, onForward0Reverse1);
		NetworkServer.RegisterHandler (CScommon.restartMsgType, onRestartMsg);
		NetworkServer.RegisterHandler (CScommon.speedMsgType, onSpeedMsg);
		NetworkServer.RegisterHandler (CScommon.broadCastMsgType, onBroadCastMsg);
		NetworkServer.RegisterHandler (CScommon.blessMsgType, onBlessMsg);

	}
	

	void OnConnectedS(NetworkMessage netMsg) 
	{	connectionIdPlayerInfo[netMsg.conn.connectionId] = new PlayerInfo();
		connectionIdPlayerInfo[netMsg.conn.connectionId].connectionId = netMsg.conn.connectionId;
		sendGameSize(netMsg.conn.connectionId);
	}



	static void checkSendToClient(int connectionId, short msgType, MessageBase msg){
		if (NetworkServer.connections[connectionId].connectionId != connectionId) { 
			if (Debug.isDebugBuild) debugDisplay("checkSend CRAZY "+connectionId+" "+NetworkServer.connections[connectionId].connectionId); 
			return;
		}
		if (NetworkServer.connections.Count <= connectionId || NetworkServer.connections[connectionId] == null ){
			if (Debug.isDebugBuild) debugDisplay("checkSend WARNING: client disconnected "+connectionId);
			return;
		}
		NetworkServer.SendToClient(connectionId, msgType, msg);
	}
	

	void sendGameSize(int connectionId){
		CScommon.GameSizeMsg gameSizeMsg = new CScommon.GameSizeMsg();
		gameSizeMsg.numNodes = Engine.nodes.Count;
		gameSizeMsg.numLinks = referenceLinkMsg.links.Length;
		gameSizeMsg.worldRadius = Bub.worldRadius;
		checkSendToClient(connectionId,CScommon.gameSizeMsgType,gameSizeMsg);
		if (Debug.isDebugBuild) debugDisplay("gameSize sent to conId "+connectionId);
	}

	// // // handlers

	public void OnDisconnectedS(NetworkMessage netMsg)
	{	int cId = netMsg.conn.connectionId;
		if (Debug.isDebugBuild) debugDisplay("Disconnection id:"+cId);

		//netMsg.conn.FlushChannels(); causes "attempt to send to not connected connection."
		netMsg.conn.Dispose(); //get rid of any existing buffers of stuff being sent? doesn't help

		//don't disconnect it... disconnect could provoke infinite recursion?
		//apparently it's already been removed from Network.connections?
		//the following line crashes with array ref out of range even w/ connectionId 1
		//Network.CloseConnection(Network.connections[netMsg.conn.connectionId],false); //network.connections is [] of NetworkPlayers, whereas netMsg.conn is a NetworkConnection...

		int nodeId = connectionIdPlayerInfo[cId].data.nodeId;
		Bots.dismount(nodeId);

		connectionIdPlayerInfo.Remove(cId);
		nodeIdPlayerInfo.Remove (nodeId);
		
		send1or2NodeNamesToAll(nodeId, "");

		checkForInitRevisions();//dismount changes dna
	}

//	public void onPush1Pull2Msg(NetworkMessage netMsg){
	//		if (paused || connectionIdPlayerInfo[netMsg.conn.connectionId].data.nodeId <0) return;
//		CScommon.intMsg push1Pull2Msg = netMsg.ReadMessage<CScommon.intMsg>();
	//		Bots.onPush1Pull2(connectionIdPlayerInfo[netMsg.conn.connectionId].data.nodeId, push1Pull2Msg.value);
//	}
	
	private void onTargetNode(NetworkMessage netMsg){
		if (paused || connectionIdPlayerInfo[netMsg.conn.connectionId].data.nodeId <0) return;

		CScommon.TargetNodeMsg targetMsg = netMsg.ReadMessage<CScommon.TargetNodeMsg>();
		//if (Debug.isDebugBuild) debugDisplay ("onTargetNode "+targetMsg.nodeIndex+" "+targetMsg.linkType);
		//		if (xing) Bots.onXTarget(connectionIdPlayerInfo[netMsg.conn.connectionId].data.nodeId, targetMsg.nodeIndex, targetMsg.linkType);
//		else 
		Bots.onTarget(connectionIdPlayerInfo[netMsg.conn.connectionId].data.nodeId, targetMsg.nodeIndex, targetMsg.linkType, targetMsg.hand);
	}

	private void onBlessMsg(NetworkMessage netMsg){
		if (paused || connectionIdPlayerInfo[netMsg.conn.connectionId].data.nodeId < 0) return;

		CScommon.intMsg blessMsg = netMsg.ReadMessage<CScommon.intMsg>();

		if (blessMsg.value < 0 || blessMsg.value >= Engine.nodes.Count) return;

		Engine.nodes[connectionIdPlayerInfo[netMsg.conn.connectionId].data.nodeId].bless(Engine.nodes[blessMsg.value]);
	}

	private void onTurnMsg(NetworkMessage netMsg){
		if (paused || connectionIdPlayerInfo[netMsg.conn.connectionId].data.nodeId <0) return;
		CScommon.intMsg intMsg = netMsg.ReadMessage<CScommon.intMsg>();
		//if (Debug.isDebugBuild) debugDisplay("turn "+intMsg.value+" on node "+connectionIdPlayerInfo[netMsg.conn.connectionId].nodeId);
		Bots.onTurn(connectionIdPlayerInfo[netMsg.conn.connectionId].data.nodeId, intMsg.value);
	}

//	private void onForward0Reverse1(NetworkMessage netMsg){
//		if (paused || connectionIdPlayerInfo[netMsg.conn.connectionId].data.nodeId <0) return;
//		CScommon.intMsg intMsg = netMsg.ReadMessage<CScommon.intMsg>();
//		Bots.onForward0Reverse1(connectionIdPlayerInfo[netMsg.conn.connectionId].nodeId, intMsg.value);
//	}

	private void onLookAtNode(NetworkMessage netMsg){
		if (paused || connectionIdPlayerInfo[netMsg.conn.connectionId].data.nodeId <0) return;
		CScommon.intMsg nixMsg = netMsg.ReadMessage<CScommon.intMsg>();
		if (Debug.isDebugBuild) debugDisplay ("onLookAtNode unimplemented"+nixMsg.value );
	}

	private void onRestartMsg(NetworkMessage netMsg){
		CScommon.intMsg intMsg = netMsg.ReadMessage<CScommon.intMsg>();
		restartGame(intMsg.value);
	}

	private static void onBroadCastMsg(NetworkMessage netMsg){
		CScommon.stringMsg chatMsg = netMsg.ReadMessage<CScommon.stringMsg>();
		chatMsg.value = connectionIdPlayerInfo[netMsg.conn.connectionId].name + ": " + chatMsg.value;
		NetworkServer.SendToAll(CScommon.broadCastMsgType, chatMsg);
	}

	private void restartGame(int v){

		if (v == 0) { togglePause(); return; }
		if (v == -1) { quitGame(currentGame); return;}

		if (v>0 && v<10) { resetDefaultScales(v); return; } //quits to a different game with its default scales

		else if (v == 21) normScaleI -= 1;
		else if (v == 22) normScaleI += 1;
		else if (v == 31) abnormScaleI -= 1;
		else if (v == 32) abnormScaleI += 1;
		else if (v == 41) photoYieldI -= 1;
		else if (v == 42) photoYieldI += 1;
		else if (v == 51) baseMetabolicRateI -= 1;
		else if (v == 52) baseMetabolicRateI += 1;
		else if (v == 61) worldRadiusI -= 1;
		else if (v == 62) worldRadiusI += 1;
		else if (v == 71) vegStartFuel -= 0.1f;
		else if (v == 72) vegStartFuel += 0.1f;
		else if (v == 81) nonvegStartFuel -= 0.1f;
		else if (v == 82) nonvegStartFuel += 0.1f;
		else if (v == 91) popcorn -= 20;
		else if (v == 92) popcorn += 20;
		setScales();
		quitGame(currentGame); // relaunches the current game
	}

	private void onSpeedMsg(NetworkMessage netMsg){
		CScommon.intMsg intMsg = netMsg.ReadMessage<CScommon.intMsg>();
		if ( connectionIdPlayerInfo[netMsg.conn.connectionId].data.nodeId < 0 ) return;
		Bots.onSpeed (Engine.nodes[connectionIdPlayerInfo[netMsg.conn.connectionId].data.nodeId], intMsg.value);
	}

	private void onInitRequest(NetworkMessage netMsg){
		CScommon.stringMsg strMsg = netMsg.ReadMessage<CScommon.stringMsg>();
		strMsg.value += netMsg.conn.connectionId; //so even if multiple identical names are requested, all names are unique
		connectionIdPlayerInfo[netMsg.conn.connectionId].name = strMsg.value;
		sendWorldToClient(netMsg.conn.connectionId);
	}

	private void onRequestNodeId(NetworkMessage netMsg){
		int oldNodeId, newNodeId, conId = netMsg.conn.connectionId;

		//all connections have an entry in connectionIdPlayerInfo, though for some the nodeId might be -1, so no need to try-catch
		oldNodeId = connectionIdPlayerInfo[conId].data.nodeId;

		CScommon.intMsg nixMsg = netMsg.ReadMessage<CScommon.intMsg>();

		if (nixMsg.value < 0 || nixMsg.value >= Engine.nodes.Count  ) newNodeId = -1;
		else newNodeId = Engine.nodes[nixMsg.value].trustHead.id; // move to the id of the head of that organism

		//enforce that only one player can mount a node.
		//if (nodeIdPlayerInfo.ContainsKey(newNodeId))newNodeId = oldNodeId;
		if (!Bots.mountable(newNodeId)) return;

		nixMsg.value = newNodeId;
		checkSendToClient(conId,CScommon.nodeIdMsgType,nixMsg);

		//both could be -1
		if (oldNodeId == newNodeId ) return; //no dna changes...
		
		//newNodeId != oldNodeId
	
		connectionIdPlayerInfo[conId].data.nodeId = newNodeId;
		connectionIdPlayerInfo[conId].clearScore();

		if (oldNodeId >= 0) {
			nodeIdPlayerInfo.Remove (oldNodeId);
			if (!Bots.dismount(oldNodeId)) if (Debug.isDebugBuild) debugDisplay("Error, onRequestNodeId oldNodeId not mounted");
		}

		if (newNodeId >= 0) {
			nodeIdPlayerInfo[newNodeId] = connectionIdPlayerInfo[conId];
			Bots.mount (newNodeId);
		}

		//case where both == -1 was weeded out above
		if (oldNodeId <0 && newNodeId>=0 ){
			send1or2NodeNamesToAll(newNodeId, connectionIdPlayerInfo[conId].name);
		}
		else if (oldNodeId >=0 && newNodeId < 0){
			send1or2NodeNamesToAll(oldNodeId, "");
		}
		else if (oldNodeId >= 0 && newNodeId >= 0){
			send1or2NodeNamesToAll(oldNodeId,"",newNodeId,connectionIdPlayerInfo[conId].name);
		}

		checkForInitRevisions(); //mounting and dismounting change dna

	}


	//allocate an initMsg to cover a segment of the total message
	private CScommon.InitMsg allocateInitMsg(int size){
		CScommon.InitMsg initMsg = new CScommon.InitMsg();
		initMsg.nodeData = new CScommon.StaticNodeData[size];
		return initMsg;
	}


	//start + initMsg.nodeData.Length must be <= Engine.nodes.Count
	private CScommon.InitMsg fillInInitMsg(CScommon.InitMsg initMsg, int start){
		initMsg.start = start;
		for (int i = start; i < start+initMsg.nodeData.Length; i++){
			Bub.Node node = Engine.nodes[i];
			//dont try to optimize ref, it makes a copy, it's a struct! CScommon.NodeData nd = initMsg.nodeData[i];
			initMsg.nodeData[i-start].radius = node.radius;
			initMsg.nodeData[i-start].dna = node.dna;
		}
		return initMsg;
	}

	private void sendInitToClient(int connectionId){
		int start = 0;
		int segmentLength = 100;


		
		while ( start+segmentLength <= Engine.nodes.Count ){
			checkSendToClient(connectionId, CScommon.initMsgType, fillInInitMsg(allocateInitMsg(segmentLength),start));
			start += segmentLength;
		}
		if (start < Engine.nodes.Count)checkSendToClient(connectionId, CScommon.initMsgType, fillInInitMsg(allocateInitMsg(Engine.nodes.Count-start),start));
	}

	private void sendWorldToClient(int connectionId){

		if (Debug.isDebugBuild) debugDisplay("Sending world to "+connectionIdPlayerInfo[connectionId].name);

		sendInitToClient(connectionId);
		sendUpdateToClient(connectionId);
		sendLinksToClient(connectionId);
		sendAllNodeNamesToClient(connectionId);
		sendAllScoresToClient(connectionId);
		                                               
	}


	private static void sendAllNodeNamesToClient(int connectionId){
		CScommon.NodeNamesMsg nnmsg = new CScommon.NodeNamesMsg();
		nnmsg.arry = new CScommon.NodeName[nodeIdPlayerInfo.Keys.Count];
		int i = 0;
		foreach (int nodeId in nodeIdPlayerInfo.Keys){
			PlayerInfo pi = nodeIdPlayerInfo[nodeId];
			if (pi.data.nodeId != nodeId) if (Debug.isDebugBuild) debugDisplay("error in sendAllNodeNames");
			nnmsg.arry[i].nodeId = pi.data.nodeId; // == nodeId
			nnmsg.arry[i].name = pi.name;
			i += 1;
		}
		checkSendToClient(connectionId,CScommon.nodeNamesMsgType,nnmsg);
	}

	private static void sendAllScoresToClient(int connectionId){
		CScommon.ScoreMsg smsg = new CScommon.ScoreMsg();
		smsg.arry = new CScommon.ScoreStruct[nodeIdPlayerInfo.Count];
		int i = 0;
		foreach (int nodeId in nodeIdPlayerInfo.Keys){
			smsg.arry[i] = nodeIdPlayerInfo[nodeId].data;
			i++;
		}
		checkSendToClient(connectionId,CScommon.scoreMsgType,smsg);
	}
	
	private static void sendScheduledScores(){
		if (scheduledScores.Count == 0) return;

		CScommon.ScoreMsg smsg = new CScommon.ScoreMsg();
		smsg.arry = new CScommon.ScoreStruct[scheduledScores.Count];

		int i=0;
		foreach (var nodeId in scheduledScores.Keys) {
			smsg.arry[i] = nodeIdPlayerInfo[nodeId].data;
			i++;
		}

		NetworkServer.SendToAll (CScommon.scoreMsgType,smsg);

		scheduledScores.Clear();
	}	


	private static void send1or2NodeNamesToAll(int nodeId0, string name0, int nodeId1=int.MaxValue, string name1 = ""){
		CScommon.NodeNamesMsg nnmsg = new CScommon.NodeNamesMsg();
		nnmsg.arry = new CScommon.NodeName[nodeId1==int.MaxValue?1:2];

		nnmsg.arry[0].nodeId = nodeId0;
		nnmsg.arry[0].name = name0;

		if (nodeId1 != int.MaxValue){
			nnmsg.arry[1].nodeId = nodeId1;
			nnmsg.arry[1].name = name1;
		}

		NetworkServer.SendToAll (CScommon.nodeNamesMsgType,nnmsg);
	}

	private static void sendScalesToAll(){
		CScommon.stringMsg scaleMsg = new CScommon.stringMsg();
		scaleMsg.value = scaleString();
		NetworkServer.SendToAll (CScommon.scaleMsgType,scaleMsg);
	}


	//allocate an updateMsg to cover a segment of the total message
	private CScommon.UpdateMsg allocateUpdateMsg(int size){
		CScommon.UpdateMsg updateMsg = new CScommon.UpdateMsg();
		updateMsg.nodeData = new CScommon.DynamicNodeData[size];
		return updateMsg;
	}

	//start + updateMsg.nodeData.Length must be <= Engine.nodes.Count
	private CScommon.UpdateMsg fillInUpdateMsg(CScommon.UpdateMsg updateMsg, int start){
		updateMsg.start = start;
		for (int i = start; i < start+updateMsg.nodeData.Length; i++){
			Bub.Node node = Engine.nodes[i];
			//dont try to optimize ref, it makes a copy, it's a struct! CScommon.NodeData nd = updateMsg.nodeData[i];
			updateMsg.nodeData[i-start].position.x = node.x;
			updateMsg.nodeData[i-start].position.y = node.y;
			updateMsg.nodeData[i-start].oomph = node.oomph;
		}
		return updateMsg;
	}

	private void sendUpdateToClient(int connectionId){
		int start = 0;
		int segmentLength = 100;
		
		while ( start+segmentLength <= Engine.nodes.Count ){
			checkSendToClient(connectionId,CScommon.updateMsgType, fillInUpdateMsg(allocateUpdateMsg(segmentLength), start));
			start += segmentLength;
		}
		
		if (start < Engine.nodes.Count)
			checkSendToClient(connectionId,CScommon.updateMsgType, fillInUpdateMsg(allocateUpdateMsg(Engine.nodes.Count-start), start));

	}

	private static string oldConnections = "";

	private void sendUpdateMsg(){

		int start = 0;
		int segmentLength = 100;

		string s = "";
		foreach (NetworkConnection conn in NetworkServer.connections){
			s += conn == null?"null":conn.connectionId+", ";
		}
		if (!s.Equals (oldConnections)){ if (Debug.isDebugBuild) debugDisplay(oldConnections + " /"); if (Debug.isDebugBuild) debugDisplay(s); oldConnections = s;}

		while ( start+segmentLength <= Engine.nodes.Count ){
			//reliable SendToAll fails with "ChannelBuffer buffer limit of 16 packets reached." if client is paused, like for dragging window around on screen
			//unity pause button: if set, sendbychanneltoall pauses editor on disconnect
			NetworkServer.SendByChannelToAll (CScommon.updateMsgType, fillInUpdateMsg(allocateUpdateMsg(segmentLength), start), Channels.DefaultUnreliable);
			start += segmentLength;
		}

		if (start < Engine.nodes.Count){
			NetworkServer.SendByChannelToAll (CScommon.updateMsgType, fillInUpdateMsg(allocateUpdateMsg(Engine.nodes.Count-start), start), Channels.DefaultUnreliable);
		}

		//for big messages, use NetworkServer.SendByChannelToAll (CScommon.updateMsgType, updateMsg, bigMsgChannel);

		checkForInitRevisions();

		checkForLinkRevisions();

		sendScheduledScores();

	}

	CScommon.StaticNodeInfo staticNodeInfoFor(int i){
		CScommon.StaticNodeInfo sni = new CScommon.StaticNodeInfo();
		sni.nodeIndex = i;
		sni.staticNodeData.radius = Engine.nodes[i].radius;
		sni.staticNodeData.dna = Engine.nodes[i].dna;
		return sni;
	}

	//referenceInitMsg.linkId, and referenceLinkJK are write once, read many times, i.e. must never change. 
	//The linkId identifies the row of the referenceMsg. Also, the sourceID of each row must never change.
	void generateReferences(){

		referenceInitMsg = fillInInitMsg(allocateInitMsg(Engine.nodes.Count),0);

		//count up all bones and muscles... bones twice.
		int linkCount = 0;
		for (int i=0;i<Engine.nodes.Count;i++){
			linkCount += Engine.nodes[i].bones.Count;
			for (int j=0; j<Engine.nodes[i].rules.Count; j++)
				linkCount += Engine.nodes[i].rules[j].musclesCount; 
		}

		referenceLinkJK = new JKStruct[linkCount];
		referenceLinkMsg = new CScommon.LinksMsg();
		referenceLinkMsg.links = new CScommon.LinkInfo[linkCount];

		int lnkcntr = 0;
		for (int i=0; i<Engine.nodes.Count;i++) { 
			for (int j=0; j<Engine.nodes[i].rules.Count; j++) for (int k = 0; k<Engine.nodes[i].rules[j].musclesCount; k++) {
				referenceLinkJK[lnkcntr] = new JKStruct(j,k);
				Bub.Muscle muscle = Engine.nodes[i].rules[j].muscles (k);
				referenceLinkMsg.links[lnkcntr].linkId = lnkcntr;
				referenceLinkMsg.links[lnkcntr].linkData.enabled = muscle.enabled;
				referenceLinkMsg.links[lnkcntr].linkData.linkType = muscle.commonType();
				referenceLinkMsg.links[lnkcntr].linkData.sourceId = muscle.source.id; // == i
				referenceLinkMsg.links[lnkcntr].linkData.targetId = muscle.target.id;
				lnkcntr += 1;
			}
			for (int j=0; j<Engine.nodes[i].bones.Count; j++) {
				referenceLinkJK[lnkcntr] = new JKStruct(j,-1);
				referenceLinkMsg.links[lnkcntr].linkId = lnkcntr;
				referenceLinkMsg.links[lnkcntr].linkData.enabled = true;
				referenceLinkMsg.links[lnkcntr].linkData.linkType = CScommon.LinkType.bone;
				referenceLinkMsg.links[lnkcntr].linkData.sourceId = Engine.nodes[i].bones[j].source.id; // == i
				referenceLinkMsg.links[lnkcntr].linkData.targetId =  Engine.nodes[i].bones[j].target.id;
				lnkcntr += 1;
			}
		}
	}

	private CScommon.LinksMsg allocateLinksMsg(int size){
		CScommon.LinksMsg linksMsg = new CScommon.LinksMsg();
		linksMsg.links = new CScommon.LinkInfo[size];
		return linksMsg;
	}

	private CScommon.LinksMsg fillInLinksMsg(CScommon.LinksMsg linksMsg, int start){
		for (int i = 0; i < linksMsg.links.Length; i++) linksMsg.links[i] = referenceLinkMsg.links[i+start];
		return linksMsg;
	}

	private void sendLinksToClient(int connectionId){
		int start = 0;
		int segmentLength = 50;
		
		while ( start+segmentLength <= referenceLinkMsg.links.Length ){
			checkSendToClient(connectionId,CScommon.linksMsgType, fillInLinksMsg(allocateLinksMsg(segmentLength), start));
			start += segmentLength;
		}
		
		if (start < referenceLinkMsg.links.Length)
			checkSendToClient(connectionId,CScommon.linksMsgType, fillInLinksMsg(allocateLinksMsg(referenceLinkMsg.links.Length-start), start));

	}


	//checkForLinkRevisions is the only consumer of referenceLinkJK content
	//which is written elsewhere, but only read here
	void checkForLinkRevisions(){
		Bub.Node source;

		List<CScommon.LinkInfo> linkInfo = new List<CScommon.LinkInfo>();

		for (int i = 0; i< referenceLinkMsg.links.Length; i++){

			source = Engine.nodes[referenceLinkMsg.links[i].linkData.sourceId];

			//bones never change
			if (referenceLinkMsg.links[i].linkData.linkType != CScommon.LinkType.bone) {
				Bub.Muscle muscle = source.rules[referenceLinkJK[i].j].muscles(referenceLinkJK[i].k);
				if ((referenceLinkMsg.links[i].linkData.targetId != muscle.target.id) ||
				    (referenceLinkMsg.links[i].linkData.enabled != muscle.enabled) ||
				    (referenceLinkMsg.links[i].linkData.linkType != muscle.commonType() )) {
					
					referenceLinkMsg.links[i].linkData.targetId =  muscle.target.id; //update reference
					referenceLinkMsg.links[i].linkData.enabled = muscle.enabled;
					referenceLinkMsg.links[i].linkData.linkType = muscle.commonType();
					
					linkInfo.Add(referenceLinkMsg.links[i]); //struct pass by copy
				}
			}
		}

		if (linkInfo.Count == 0) return;

		int start = 0;
		int segmentLength = 50;
		
		while ( start+segmentLength <= linkInfo.Count ){
			CScommon.LinksMsg linksMsg = allocateLinksMsg(segmentLength);
			for (int i = 0; i<segmentLength; i++) linksMsg.links[i] = linkInfo[start+i];
			NetworkServer.SendToAll(CScommon.linksMsgType,linksMsg);
			start += segmentLength;
		}

		if (start < linkInfo.Count) {
			CScommon.LinksMsg linksMsg = allocateLinksMsg(linkInfo.Count - start);
			for (int i = 0; i < linkInfo.Count - start; i++) linksMsg.links[i] = linkInfo[start+i];
			NetworkServer.SendToAll(CScommon.linksMsgType,linksMsg);
		}

	}

	void checkForInitRevisions(){

		List<CScommon.StaticNodeInfo> nodeInfoList = new List<CScommon.StaticNodeInfo>();

		for (int i=0; i< referenceInitMsg.nodeData.Length; i++)
		if (Engine.nodes[i].dna != referenceInitMsg.nodeData[i].dna || Engine.nodes[i].radius != referenceInitMsg.nodeData[i].radius) {
				nodeInfoList.Add(staticNodeInfoFor(i));
				referenceInitMsg.nodeData[i] = nodeInfoList[nodeInfoList.Count-1].staticNodeData; //update reference
			}
		
		if (nodeInfoList.Count == 0) return;

		if (nodeInfoList.Count > 80) if (Debug.isDebugBuild) debugDisplay("Warning, initRevision may need segmentation: "+nodeInfoList.Count);

		//copy list into an array. Messages can't contain complex classes or generic containers
		//(see http://docs.unity3d.com/Manual/UNetMessages.html )

		CScommon.InitRevisionMsg initRevisionMsg = new CScommon.InitRevisionMsg();
		initRevisionMsg.nodeInfo = new CScommon.StaticNodeInfo[nodeInfoList.Count];

		for (int i=0; i< nodeInfoList.Count; i++) initRevisionMsg.nodeInfo[i] = nodeInfoList[i];

		NetworkServer.SendToAll(CScommon.initRevisionMsgType,initRevisionMsg);
		//safeSendToClient(1, CScommon.initRevisionMsgType,initRevisionMsg); ///888888888
	}
	
	//for client, check comment by aabramychev about Receive() near bottom of
	// http://forum.unity3d.com/threads/how-to-set-the-qos-in-llapi.326196/
}
