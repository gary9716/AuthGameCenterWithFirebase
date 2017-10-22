using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using AOT;
using System;
using System.Text;
using UnityEngine.Networking;
using GameCenter.Auth;

public class GameCenterAuthWithFireBaseTest : MonoBehaviour {

	protected Firebase.Auth.FirebaseAuth auth;

	public string myAuthServerAddr = "127.0.0.1";
	public int myAuthServerPort = 8080;
	public string gcAuthPath = "/verify/gamecenter";


	class TokenData {
		public string status;
		public string token;
	}

#region GameCenterAuth

	class GCAuthData {
		public string publicKeyUrl;
		public ulong timestamp;
		public string signature;
		public string salt;
		public string playerId;
		public string alias;
		public string bundleId;
	}


	[MonoPInvokeCallback(typeof(GameCenterSignature.OnSucceeded))]
	private void OnGeneratingSucceed(
		string PublicKeyUrl, 
		ulong timestamp,
		string signature,
		string salt,
		string playerID,
		string alias,
		string bundleID)
	{
		Debug.Log("Succeeded authorization to gamecenter: \n" +
			"PublicKeyUrl=" + PublicKeyUrl + "\n" +
			"timestamp=" + timestamp + "\n" +
			"signature=" + signature + "\n" + 
			"salt=" + salt + "\n" +
			"playerID=" + playerID + "\n" +
			"alias=" + alias + "\n" +
			"bundleID=" + bundleID);

		GCAuthData gcAuthData = new GCAuthData();
		gcAuthData.publicKeyUrl = PublicKeyUrl;
		gcAuthData.timestamp = timestamp;
		gcAuthData.signature = signature;
		gcAuthData.salt = salt;
		gcAuthData.playerId = playerID;
		gcAuthData.alias = alias;
		gcAuthData.bundleId = bundleID;

		string jsonStr = JsonUtility.ToJson(gcAuthData);
		StartCoroutine(UploadDataForVerification(jsonStr, onResponse));

	}

	[MonoPInvokeCallback(typeof(GameCenterSignature.OnFailed))]
	private void OnGeneratingFailed(string reason)
	{
		Debug.Log("Failed to authenticate with gamecenter:" + reason);
	}

	private void OnLocalAuthenticateResult(bool success)
	{
		if (success)
		{
			Debug.Log("LocalAuthenticate success!");
			GameCenterSignature.Generate(OnGeneratingSucceed, OnGeneratingFailed);
		}
		else
		{
			Debug.Log("LocalAuthentificate failed.");
		}
	}

	public void Auth()
	{
		if (Social.localUser.authenticated)
		{
			GameCenterSignature.Generate(OnGeneratingSucceed, OnGeneratingFailed);
		}
		else
		{
			Social.localUser.Authenticate(OnLocalAuthenticateResult);
		}
	}
	delegate void UploadResponseHandler(TokenData resData);

	IEnumerator UploadDataForVerification(string data, UploadResponseHandler handler) {
		string serverAddr = "http://" + myAuthServerAddr + ":" + myAuthServerPort + gcAuthPath;
		UnityWebRequest www = UnityWebRequest.Put(serverAddr, data);
		
		yield return www.Send();

		if(www.isError) {
            Debug.Log(www.error);
        }
        else {
			try {
				TokenData tokenData = JsonUtility.FromJson<TokenData>(www.downloadHandler.text);
				handler(tokenData);
			}
			catch(Exception e) {
				Debug.Log("parsed token failed:" + e);
				handler(null);
			}
		}
	}

	void onResponse(TokenData data) {
		if(data != null && data.status == "succeed") {
			//use token to authenticate with firebase
			auth.SignInWithCustomTokenAsync(data.token);
		}
	}

	void SendTestData() {
		GCAuthData gcAuthData = new GCAuthData();
		gcAuthData.publicKeyUrl = "fakeUrl";
		gcAuthData.timestamp = 1234;
		gcAuthData.signature = "fakeSign";
		gcAuthData.salt = "fakeSalt";
		gcAuthData.playerId = "fakePlayer";
		gcAuthData.alias = "fakeAlias";
		gcAuthData.bundleId = "com.fake.test";

		string jsonStr = JsonUtility.ToJson(gcAuthData);
		StartCoroutine(UploadDataForVerification(jsonStr, onResponse));
	}

#endregion

	Firebase.DependencyStatus dependencyStatus = Firebase.DependencyStatus.UnavailableOther;

	// Use this for initialization
	void Start () {
		//init Firebase SDK
		Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
			dependencyStatus = task.Result;
			if (dependencyStatus == Firebase.DependencyStatus.Available) {
				InitializeFirebase();
			} else {
				Debug.LogError(
				"Could not resolve all Firebase dependencies: " + dependencyStatus);
			}
		});

	}
	
	void InitializeFirebase() {
		Debug.Log("Setting up Firebase Auth");
		auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
		auth.StateChanged += AuthStateChanged;
		auth.IdTokenChanged += IdTokenChanged;
		
		//SendTestData();
  	}

	void OnDestroy() {
		auth.StateChanged -= AuthStateChanged;
		auth.IdTokenChanged -= IdTokenChanged;
		auth = null;
		
	}

	// Track state changes of the auth object.
  void AuthStateChanged(object sender, System.EventArgs eventArgs) {
    Firebase.Auth.FirebaseAuth senderAuth = sender as Firebase.Auth.FirebaseAuth;
    Firebase.Auth.FirebaseUser user = null;
    if (senderAuth == auth && senderAuth.CurrentUser != user) {
      bool signedIn = user != senderAuth.CurrentUser && senderAuth.CurrentUser != null;
      if (!signedIn && user != null) {
        Debug.Log("Signed out " + user.UserId);
      }
      user = senderAuth.CurrentUser;
      if (signedIn) {
        Debug.Log("Signed in " + user.UserId);
      }
    }
  }

  // Track ID token changes.
  void IdTokenChanged(object sender, System.EventArgs eventArgs) {
    Firebase.Auth.FirebaseAuth senderAuth = sender as Firebase.Auth.FirebaseAuth;
    if (senderAuth == auth && senderAuth.CurrentUser != null) {
      senderAuth.CurrentUser.TokenAsync(false).ContinueWith(
        task => Debug.Log(String.Format("Token[0:8] = {0}", task.Result.Substring(0, 8))));
    }
  }
}
