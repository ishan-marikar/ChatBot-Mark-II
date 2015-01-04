using System;
using System.Text;
using ChatterBotAPI;
using agsXMPP;
using agsXMPP.protocol.client;
using System.Collections.Generic;
using System.IO;

namespace ChatBotMarkII
/*
 * 8888888         888                             888b     d888                  d8b 888                       
 *   888           888                             8888b   d8888                  Y8P 888                       
 *   888           888                             88888b.d88888                      888                       
 *   888  .d8888b  88888b.   8888b.  88888b.       888Y88888P888  8888b.  888d888 888 888  888  8888b.  888d888 
 *   888  88K      888 "88b     "88b 888 "88b      888 Y888P 888     "88b 888P"   888 888 .88P     "88b 888P"   
 *   888  "Y8888b. 888  888 .d888888 888  888      888  Y8P  888 .d888888 888     888 888888K  .d888888 888     
 *   888       X88 888  888 888  888 888  888      888   "   888 888  888 888     888 888 "88b 888  888 888     
 * 8888888 88888P' 888  888 "Y888888 888  888      888       888 "Y888888 888     888 888  888 "Y888888 888       
 * 
 *  Ishan Marikar (ishan.marikar@outlook.com) 
 *  This project is based on: http://www.codeproject.com/Tips/712022/Using-CleverBOT-as-Your-Secretary-on-Facebook,
 *							  http://www.primaryobjects.com/cms/article130.aspx &
 *							  http://csharp-tricks-en.blogspot.com/2013/10/read-out-facebook-friend-list.html
 *  
*/
{
	class Program
	{
		// Class variables declared globally to store the instances among all the methods
		static XmppClientConnection xmppClient;
		static ChatterBotSession chatbotSession;
		static TextWriter logFileWriter;

		// These boolean variables store the status of the friend collection
		private static bool startCollectingContacts = false;
		private static bool collectingContacts = true;

		// .. and this is where we store the friends.
		static Dictionary<string, string> Friends = new Dictionary<string, string>();

		//  The server URI and port needed to connect to Facebook's Chat XMPP server
		
		//private const string SERVER = "chat.facebook.com";

		private const string Server = "173.252.75.17";
		private const int StandardPort = 5222;
		// .. in case the firewall blocks ports.
		private const int FallbackPort = 443;
		// I'd probably try to make it connect to the fallback port if the standard port
		// if the connections fails, but I have to figure out how how I should implement
		// this properly.

		static void Main(string[] args)
		{
			// We instantiate the class and create a connection with the SERVER details ..
			
			xmppClient = new XmppClientConnection(Server, FallbackPort)
			{ 
				// .. and the login details. You can usually find the username (if you normally
				// use an email to login by going to your profile and checking the URL.
				// It would looks something similar to facebook.com/your-username.
				// If not, then go to your profile details and look for the email given
				// to you by facebook, that could work too.
				Username = "your-username",
				Password = "your-password",
				// This is useful so I don't have to manually set the presence as online
				// once you login. Hopefully this is what it does. I'm not entirely sure
				// what it does yet because I am yet to read the documentation.
				AutoPresence = true,
			};

			// We instantiate the chatbot here. You can find the API and any samples on
			// https://code.google.com/p/chatter-bot-api/
			ChatterBotFactory chatbotFactory = new ChatterBotFactory();
			ChatterBot chatbot = chatbotFactory.Create(ChatterBotType.CLEVERBOT);
			//ChatterBot chatbot = chatbotFactory.Create(ChatterBotType.PANDORABOTS, "94ade513be34ddcf");
			chatbotSession = chatbot.CreateSession();
			// Note: All the chats here use the same instance, so it might reply to someone else in the 
			//		 context of the subject you were talking about. I'll try to fix this soon.

			// We subscribe to the events and run the relevant methods
			xmppClient.OnMessage += xmppClient_OnMessage;
			xmppClient.OnError += xmppClient_OnError;
			xmppClient.OnClose += xmppClient_OnClose;
			xmppClient.OnLogin += xmppClient_OnLogin;
			xmppClient.OnSocketError += xmppClient_OnError;

			// Here we go! :3 Catch us if anything goes wrong. :c
			try
			{
				// This can be helpful
				PrintMessage(string.Format("[+] Attempting to connect to {0} on port {1} ..", xmppClient.Server, xmppClient.Port), ConsoleColor.Yellow);
				// .. and we finally open the connection.
				xmppClient.Open();
			}
			// Murphey's Law: What can go wrong, Will go wrong.
			catch (Exception ex)
			{
				PrintError(ex);
				try
				{
					if (xmppClient.Port == StandardPort)
					{
						xmppClient.Port = FallbackPort;
					}
					else if(xmppClient.Port == FallbackPort)
					{
						xmppClient.Port = StandardPort;
					}
					PrintMessage(string.Format("[+] Attempting to connect to {0} on fallback port {1} ..", xmppClient.Server, xmppClient.Port), ConsoleColor.Yellow);
					xmppClient.Open();
				}
				catch (Exception ex2)
				{
					PrintError(ex2);
				}
			}

			// Starting collecting the roster and put the program in an infinite sleep loop,
			// (Most of the functions are asynchronous) so it won't close.
			while (true)
			{
				while (collectingContacts)
				{
					if (startCollectingContacts)
						collectingContacts = false;
					System.Threading.Thread.Sleep(1000);
				}
			}	


		}
		// This happens once we login. This gets the jid and name of the friends in the roster and 
		// stores them in the 'Friends' dictionary.
		static void xmppClient_OnRosterItem( object sender, agsXMPP.protocol.iq.roster.RosterItem item )
		{
			startCollectingContacts = true;
			collectingContacts = true;
			Friends.Add(item.GetAttribute("jid"), item.GetAttribute("name"));

		}

		// This runs once we log in to the system.
		static void xmppClient_OnLogin( object sender )
		{
			// Are we really logged in?
			if (xmppClient.Authenticated)
			{
				// Yes! c:
				PrintMessage("[*] Client authenticated with server.\n", ConsoleColor.Green);
				PrintMessage(string.Format("[*] Your JID is: {0}. \n", xmppClient.MyJID), ConsoleColor.Green);

				xmppClient.OnRosterItem += xmppClient_OnRosterItem;
				xmppClient.OnRosterEnd += tempClientInstance_OnRosterEnd;

			}
			else
			{
				// No :c
				PrintMessage("[!] Client could not authenticate with the server.\n", ConsoleColor.Red);
			}
		}

		// Once we fill the roster with al our friends, we run this method and show the message
		static void tempClientInstance_OnRosterEnd( object sender )
		{

			PrintMessage(string.Format("[*] {0} friends in the roster.", Friends.Count), ConsoleColor.Magenta);
		}

		// This runs when the connection closes (hopefully!)
		static void xmppClient_OnClose( object sender )
		{
			PrintMessage("[!] The connection closed.", ConsoleColor.Red);
		}

		// If we were to have any kind of errors, this method would run and log the error to the console.
		static void xmppClient_OnError( object sender, Exception ex )
		{
			PrintError(ex);
		}

		// Yaay! We got a message! :D This is where the fun happens.
		static void xmppClient_OnMessage( object sender, Message msg )
		{
			// Transfer the sender and the message into separate variables
			Jid currentUser = msg.From;
			string message = msg.Body;
			// .. and also search for the real name of the person by cross referencing
			// the dictionary we made earlier.
			string facebookName = Friends[currentUser.Bare];
			// If we wanted to the opposite of this (get the JID from a name), it would be:
			// string currentUserJid = Friends.First(x => x.Value == "Ishan Marikar").Key;

			// If the user is still typing ..
			if (msg.Chatstate == agsXMPP.protocol.extensions.chatstates.Chatstate.composing)
			{
				// Just print this.. 
				PrintMessage(string.Format("\n[*] {0} is typing.. ", facebookName), ConsoleColor.Green);
				// .. and get out.
				return;
			}

			// If the message is NOT empty, then we run this:
			if (!string.IsNullOrEmpty(message))
			{
				// Print the message we've recieved along with the user who sent it.
				PrintMessage(string.Format("\n[+] {0} to Chatbot: {1}", facebookName, message), ConsoleColor.Yellow);

				// Pass the message to the chatbot and get the reply..
				string messageReply = chatbotSession.Think(message);

				// .. and get send the message back to the user who sent it.
				Message newMessage = new Message(currentUser, MessageType.chat, messageReply);
				xmppClient.Send(newMessage);

				PrintMessage(string.Format("[+] ChatBot to {1}: {0}\n", messageReply, facebookName), ConsoleColor.White);
			}
			

		}

		// There's nothing much here, I've just offloaded the work of constantly changing 
		// the foreground colour for a single message, to a method.
		private static void PrintMessage(string message, ConsoleColor colour)
		{
			ConsoleColor originalColour = Console.ForegroundColor;
			Console.ForegroundColor = colour;
			Console.WriteLine(message);
			Console.ForegroundColor = originalColour;
		}

		// The same as above, but for exceptions.
		private static void PrintError(Exception ex)
		{
			ConsoleColor originalColour = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("[!] An error occured: {0}", ex.Message);
			Console.ForegroundColor = originalColour;
		}


		 private void OpenLog(string message)
		{
			TextWriter logFileWriter;
			string logFile = @"chatMessages.log";
			if (!File.Exists(logFile))
			{
				File.Create(logFile);
				logFileWriter = new StreamWriter(logFile);
			}
			else
			{
				logFileWriter = new StreamWriter(logFile);
				logFileWriter.WriteLine("{0} : {1}", DateTime.Now.ToLongDateString(), message);
			}

		}

	}
}
