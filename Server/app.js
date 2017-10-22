const express = require('express');
const rp = require('request-promise');
const bodyParser = require('body-parser');
const app = express();
const port = 8080;
const gcVerifier = require('gamecenter-identity-verifier');

var admin = require("firebase-admin");
var serviceAccount = require("./serverCer.json");

admin.initializeApp({
  credential: admin.credential.cert(serviceAccount),
  databaseURL: "https://testfirebase-d3ade.firebaseio.com"
});

var createCustomTokenAndSendBack = function(res, uid) {
    var additionalClaims = {
        provider: "gamecenter" 
    };
    admin.auth().createCustomToken(uid, additionalClaims)
    .then(function(customToken) {
        // Send token back to client
        res.send({
            status: "succeed",
            token: customToken
        });
    })
    .catch(function(error) {
        console.log("Error creating custom token:", error);
        res.send({
            status: "failed",
            token: ""
        });
    });
}

function getFirebaseUser(identity) {
    // Generate Firebase user's uid based on GameCenter's playerID
    const firebaseUid = `gamecenter:${identity.playerId}`;
    return admin.auth().getUser(firebaseUid).catch(error => {
      // If user does not exist, create a Firebase new user with it
      if (error.code === 'auth/user-not-found') {
        const displayName = identity.alias;
        
        console.log('Create new Firebase user');
        // Create a new Firebase user and return it
        return admin.auth().createUser({
            uid: firebaseUid,
            displayName: displayName
        });
      }
      // If error other than auth/user-not-found occurred, fail the whole login process
      throw error;
    });
  }

function bypass(identity, res) {
    getFirebaseUser(identity)
    .then(function(userRecord) {
        createCustomTokenAndSendBack(res,userRecord.uid)
    });
}

app.put('/verify/gamecenter', bodyParser.raw(), function (req, res) {
    
    try{
        var identity = JSON.parse(req.body);
        //bypass(identity, res);
        //return;

        gcVerifier.verify(identity, function (err, token) {
            if (!err) {
                console.log("gc token:" + token);
                getFirebaseUser(identity)
                .then(function(userRecord) {
                    createCustomTokenAndSendBack(res,userRecord.uid)
                });
            }
            else {
                console.log("verification failed");
                res.send({
                    status: "failed",
                    token: ""
                });
            }
          });
    
    }
    catch(e) {
        console.log("server error:");
        console.log(e);
        res.send({
            status: "failed",
            token: ""
        });
    }    
});

app.listen(port, function () {
  console.log('server listening on port 8080.');
});