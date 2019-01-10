using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ScottFreeLoader
{
    /// <summary>
    /// Contains the unpacked and processed game data from a .DAT file
    /// </summary>
    public partial class GameData
    {

        public GameHeader Header;
        public GameFooter Footer;
        public Room[] Rooms = null;
        public Action[] Actions = null;
        public Item[] Items = null;
        public string[] Verbs = null;
        public string[] Nouns = null;
        public string[] Messages = null;
        public string GameName = null;

        private string CurrentFolder
        {
            get
            {
                return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
        }

        #region public instance methods

        public GameData()
        {

        }

      

        #endregion

        #region public static methods

      

        /// <summary>
        /// Load the adventure game from the provided dat file
        /// </summary>
        /// <param name="pFile"></param>
        /// <returns>Game data class</returns>
        public static GameData Load(string pFile)
        {
            string[] directionsLong = { "North", "South", "East", "West", "Up", "Down" };

            GameData gd = new GameData();

            DATToChunks.Load(pFile);

            int[] header = DATToChunks.GetTokensAsInt(12);

            gd.Header = new GameHeader(header);
            gd.Verbs = new string[gd.Header.NumNounVerbs];
            gd.Nouns = new string[gd.Header.NumNounVerbs];
            gd.Rooms = new Room[gd.Header.NumRooms];
            gd.Messages = new string[gd.Header.NumMessages];
            gd.Items = new Item[gd.Header.NumItems];
            gd.GameName = pFile;

            int ctr = 0;

            List<Action> Actions = new List<Action>();

            #region Actions

            for (ctr = 0; ctr < gd.Header.NumActions; ctr++)
                Actions.Add(new Action(DATToChunks.GetTokensAsInt(8)));

            #endregion

            #region Words

            /*
             * An interleaved list of verb/noun that begins
             * with the entries "AUT" and "ANY" that we skip
             * 
             * An entry beginning with a star is a synonym of the first
             * preceeding word that doesn't begin with a star
             */

            int v = 0;
            int n = 0;
            string[] word = DATToChunks.getTokens(gd.Header.NumNounVerbs * 2);

            for (ctr = 0/*SKIP*/; ctr < word.Count(); ctr++)
            {

                if (ctr % 2 == 0)
                {

                    gd.Verbs[v] = word[ctr];

                    if (gd.Verbs[v].StartsWith("*") & gd.Verbs[v].Length > (gd.Header.WordLength + 1))
                        gd.Verbs[v] = gd.Verbs[v].Substring(0, gd.Header.WordLength + 1);
                    else if (!gd.Verbs[v].StartsWith("*") & gd.Verbs[v].StartsWith("*") && word[ctr].Length > gd.Header.WordLength)
                        gd.Verbs[v] = gd.Verbs[v].Substring(0, gd.Header.WordLength);

                    v++;
                }
                else
                {
                    gd.Nouns[n] = word[ctr];

                    if (gd.Nouns[n].StartsWith("*") & gd.Nouns[n].Length > (gd.Header.WordLength + 1))
                        gd.Nouns[n] = gd.Nouns[n].Substring(0, gd.Header.WordLength + 1);
                    else if (!gd.Nouns[n].StartsWith("*") & word[ctr].Length > gd.Header.WordLength)
                        gd.Nouns[n] = gd.Nouns[n].Substring(0, gd.Header.WordLength);

                    n++;
                }

            }

            #endregion

            #region Rooms

            for (ctr = 0; ctr < gd.Rooms.Length; ctr++)
                gd.Rooms[ctr] = new Room(DATToChunks.GetTokensAsInt(6), DATToChunks.getTokens(1).First());

            #endregion

            #region Build Game Messages

            gd.Messages = DATToChunks.getTokens(gd.Messages.Length);

            #endregion

            #region Items

            for (ctr = 0; ctr < gd.Items.Length; ctr++)
                gd.Items[ctr] = new Item(DATToChunks.getTokens(1).First(), DATToChunks.GetTokensAsInt(1).First());

            #endregion

            #region Add any comments to actions

            for (ctr = 0; ctr < gd.Header.NumActions; ctr++)
                Actions[ctr].Comment = DATToChunks.getTokens(1).First();

            #endregion


            //      Child action processing

            //      All actions that follow an action with a component 73 are noun == 0 && verb == 0
            //      and are the children of that action. This method moves them into a array of
            //      their parent
            //      e.g 157 in Adv01.dat
            List<Action> childs = null;
            for (int i = Actions.Count() - 1; i >= 0; i--)
            {
                if (Actions[i].Actions.Count(act => act[0] == 73) > 0)
                {
                    int j = i + 1;
                    childs = new List<Action>();
                    while (Actions[j].Verb == 0 && Actions[j].Noun == 0)
                    {
                        childs.Add(Actions[j]);
                        j++;
                    }

                    Actions[i].Children = childs.ToArray();
                    Actions.RemoveRange(i + 1, childs.Count());
                }
            }

            /*
             Claymorgue castle fix

             Action 148 has no conditions and a noun and a verb of 0, 
             and follows a user tiggered action. This particular set of 
             conditions only occurs in Claymorgue and none of the other 13
             adventures. I think that when this set of conditions occurs,
             the latter action should be treated as a child of the former, 
                and would require some special treatment when the DAT file is loaded.
            */
            Action ac;
            for (var i = 1; i < Actions.Count(); i++)
            {
                ac = Actions[i];

                if (
                        Actions[i].Conditions.Length == 0
                        && Actions[i].Actions.Length > 0
                        && Actions[i].Verb == 0
                        && Actions[i].Noun == 0
                        && Actions[i - 1].Verb > 0
                    )
                {

                    Actions[i - 1].Children =
                        new Action[] { Actions[i] };

                    Actions[i] = null;
                }
            }

            //If words aren't present for SAV GAM add them
            //for adv06, 07, 09, 10, 11, 12, 13, 14a
            #region Add save game

            if (!gd.Nouns.Contains("GAM") && !gd.Verbs.Contains("SAV"))
            {

                Array.Resize(ref gd.Nouns, gd.Nouns.Length + 1);
                gd.Nouns[gd.Nouns.Length - 1] = "GAM";

                Array.Resize(ref gd.Verbs, gd.Verbs.Length + 1);
                gd.Verbs[gd.Verbs.Length - 1] = "SAV";

                Actions.Add(
                        new Action(
                                new int[]
                                {
                                    (gd.Verbs.Length - 1) * 150     //verb
                                    + (gd.Nouns.Length - 1)   //noun
                                    , 0 //condition 1
                                    , 0 //condition 2
                                    , 0 //condition 3
                                    , 0 //condition 4
                                    , 0 //condition 5
                                    , 71 * 150
                                    , 0
                                }
                            )
                    );

            }

            #endregion

            gd.Actions = Actions.Where(a => a != null).ToArray();

            gd.Footer = new GameFooter(DATToChunks.GetTokensAsInt(3));



            return gd;

        }

        #endregion


        #region game structure classes

        public class GameHeader
        {
            public GameHeader(int[] pVals)
            {
                Unknown = pVals[0];
                NumItems = pVals[1] + 1;
                NumActions = pVals[2] + 1;
                NumNounVerbs = pVals[3] + 1;
                NumRooms = pVals[4] + 1;
                MaxCarry = pVals[5];
                StartRoom = pVals[6];
                TotalTreasures = pVals[7];
                WordLength = pVals[8];
                LightDuration = pVals[9];
                NumMessages = pVals[10] + 1;
                TreasureRoom = pVals[11];
            }

            public int Unknown { get; private set; }
            public int NumItems { get; private set; }
            public int NumActions { get; private set; }
            public int NumNounVerbs { get; private set; }
            public int NumRooms { get; private set; }
            public int MaxCarry { get; private set; }
            public int StartRoom { get; private set; }
            public int TotalTreasures { get; private set; }
            public int WordLength { get; private set; }
            public int LightDuration { get; private set; }
            public int NumMessages { get; private set; }
            public int TreasureRoom { get; private set; }
        }

        public class GameFooter
        {
            public GameFooter(int[] pVals)
            {
                Version = pVals[0];
                AdventureNumber = pVals[1];
                Unknown = pVals[2];
            }

            public int Version { get; set; }
            public int AdventureNumber { get; set; }
            public int Unknown { get; set; }
        }


        public class Room
        {
            public Room(int[] pExits, string pDescription)
            {
                Description = pDescription;
                Exits = pExits;
            }

            public string Description { get; set; }
            public string RawDescription { get; set; }
            public int[] Exits { get; private set; }

            /// <summary>
            /// Output the class as it's corresponding DAT file entry
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return string.Format("\"{0}\"\r\n{1}", Description, Exits.Select(i => i.ToString() + "\r\n"));
            }

        }

        public class Item
        {
            public Item(string pDescription, int pLocation)
            {
                //an item description contains an associated word if a slash if present
                //this means the item can be taken and dropped
                string[] val = pDescription.Split(new char[] { '/' });

                Description = val[0];
                Location = pLocation;
                OriginalLocation = pLocation;
                if (val.Length > 1)
                    Word = val[1];
                else
                    Word = null;
            }

            public void Reset()
            {
                Location = OriginalLocation;
            }

            public bool Moved()
            {
                return OriginalLocation != Location;
            }

            public string Description { get; private set; }
            public int Location { get; set; }
            private int OriginalLocation { get; set; }

            /// <summary>
            /// If an item has one of these it can be picked up without a special action 
            /// created for it
            /// </summary>
            public string Word { get; private set; }

            public override string ToString()
            {
                return String.Format("\"{0}{1}\" {2}", Description, String.IsNullOrEmpty(Word) ? "" : "/" + Word + "/", OriginalLocation);
            }
        }

        public class Action
        {
            public Action()
            {

            }

            public Action(int[] pData)
            {
                Comments = new List<string>();

                /*
                    8 item integer array representing an action

                    [0] verb/noun
                    [1 - 5] conditons
                    [6 - 7] actions

                */

                Verb = pData[0] / 150;
                Noun = pData[0] % 150;

                //5 conditions
                Conditions = pData.Skip(1)
                                    .Take(5)
                                    //.Where(con => con % 20 > 0)
                                    .Select(con => con % 20 > 0
                                                    ? new int[] { con % 20, con / 20 }
                                                    : new int[] { 0, 0 })
                                    .ToArray();


                //action args are stored in conditions
                int[] actarg = pData.Skip(1)
                                    .Take(5)
                                    .Where(con => con % 20 == 0)
                                    .Select(con => con / 20)
                                    .ToArray();



                //get all four arguments
                Actions = pData
                                 .Skip(6)
                                 .Take(2)
                                   //.Where(val => val > 0)
                                   .Select(val =>
                                            val > 0 ?
                                                val % 150 > 0
                                                ? new int[][] { new int[] { val / 150, 0, 0 }, new int[] { val % 150, 0, 0 } }
                                                : new int[][] { new int[] { val / 150, 0, 0 }, new int[] { 0, 0, 0 } }
                                            : new int[][] { new int[] { 0, 0, 0 }, new int[] { 0, 0, 0 } })
                                   .SelectMany(val => val)
                                   .ToArray();


                int aaPos = 0;
                //asign the action args to the action
                foreach (int[] a in Actions)
                {
                    switch (a[0])
                    {
                        //require 1 argument
                        case 52:
                        case 53:
                        case 54:
                        case 55:
                        case 58:
                        case 59:
                        case 60:
                        case 74:
                        case 81:
                        case 82:
                        case 83:
                        case 87:
                        case 79:    //set current counter
                            a[1] = actarg[aaPos];
                            aaPos++;
                            break;

                        //actipons that require 2 args
                        case 62:
                        case 72:
                        case 75:
                            a[1] = actarg[aaPos];
                            a[2] = actarg[aaPos + 1];
                            aaPos += 2;
                            break;
                    }
                }

            }

            public int Verb { get; set; }
            public int Noun { get; set; }
            public int[][] Conditions { get; set; }
            public int[][] Actions { get; set; }
            public string Comment { get; set; }
            public Action[] Children { get; set; }
            List<string> Comments;

            /// <summary>
            /// Output the action as a scott free format
            /// </summary>
            /// <returns></returns>
            private int[] ToDat()
            {
                int VN = Verb * 150 + Noun;

                int[] con = Conditions
                                .Select((val, ind) => val[0] > 0
                                                        ? val[0] + val[1] * 20
                                                        : 0)
                                .ToArray();


                int[] args = Actions
                                .SelectMany(a => a.Skip(1))
                                .Where(a => a > 0)
                                .Select(a => a * 20)
                                .ToArray();

                //add the args back into the empty condition blocks
                int argctr = 0;
                if (args.Count(a => a > 0) > 0)
                {
                    for (int ctr = 0; ctr < con.Length; ctr++)
                    {
                        if (con[ctr] == 0)
                        {
                            con[ctr] = args[argctr];
                            argctr++;
                        }
                        if (argctr > args.Count() - 1)
                            break;
                    }
                }

                int[] acts =
                    {
                        Actions[0][0] > 0
                            ? Actions [0][0] * 150 + Actions[1][0]
                            : 0
                        ,

                        Actions[2][0] > 0
                            ? Actions [2][0] * 150 + Actions[3][0]
                            : 0

                    };

                return new int[]
                    {
                        VN
                        , con[0]
                        , con[1]
                        , con[2]
                        , con[3]
                        , con[4]
                        , acts[0]
                        , acts[1]

                    };


            }

            public override string ToString()
            {
                return String.Join("\r\n", ToDat().Select(c => c.ToString()));

            }


        }
        #endregion


    }
}
