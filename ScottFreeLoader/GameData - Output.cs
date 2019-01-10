using System;
using System.Linq;
using System.Xml.Linq;

namespace ScottFreeLoader
{
    public partial class GameData
    {
        /// <summary>
        /// Convert the loaded game DAT file into XML with accompanying comments
        /// </summary>
        public XElement XMLOutput()
        {
            XElement gameData = new XElement("GameData");
            gameData.Add(
                       new XElement("Header"
                            , new XElement("FileName", GameName)
                            , new XElement("Unknown", Header.Unknown)
                            , new XElement("MaxCarry", Header.MaxCarry)
                            , new XElement("StartRoom", Header.StartRoom)
                            , new XElement("TotalTreasures", Header.TotalTreasures)
                            , new XElement("WordLength", Header.WordLength)
                            , new XElement("LightDuration", Header.LightDuration)
                            , new XElement("TreasureRoom", Header.TreasureRoom)
                       )
                );

            //
            gameData.Add(new XElement("Actions"
                    , new XAttribute("Count", Actions.Count())
                    , Actions.Select((val, ind) => MakeAction(val, ind, false)))
                );


            //add words
            gameData.Add(
                   new XElement("Words"

                        , new XElement("Verbs", new XAttribute("Count", Verbs.Count())
                        , Verbs.Select((val, ind) => new { index = ind, value = val })
                            .Where(v => !v.value.StartsWith("*"))
                            .Select(v =>
                                new XElement("Verb", new object[] { new XAttribute("value", v.value), new XAttribute("index", v.index)
                                , from al in Verbs.Skip(v.index + 1).TakeWhile(al => al.StartsWith("*")) select new XElement("Alias", al) }
                                ))
                                )
                        , new XElement("Nouns", new XAttribute("Count", Nouns.Count())
                        , Nouns.Select((val, ind) => new { index = ind, value = val })
                            .Where(v => !v.value.StartsWith("*") & v.value.Length > 0)
                            .Select(v =>
                                new XElement("Noun", new object[] { new XAttribute("value", v.value), new XAttribute("index", v.index)
                                , from al in Nouns.Skip(v.index + 1).TakeWhile(al => al.StartsWith("*")) select new XElement("Alias", al) }
                                ))
                            )
                            )
                );

            //add rooms
            string[] directionsLong = { "North", "South", "East", "West", "Up", "Down" };
            gameData.Add(
                   new XElement("Rooms"
                   , new XAttribute("Count", Rooms.Count())
                        , Rooms.Select((val, ind) => new XElement("Room",
                            new object[] {
                                new XAttribute("index", ind)
                                , new XElement("Description", val.RawDescription)
                                , val.Exits.Select((v,i)=> new XElement(directionsLong[i], v))
                            })))

                );


            //messaes
            gameData.Add(
                new XElement("Messages"
                , new XAttribute("Count", Messages.Count())
                , Messages.Select((val, ind) => new XElement("Message",
                new object[] {
                                new XAttribute("index", ind)
                                , val
                })))
                );

            gameData.Add(
                new XElement("Items"
                , new XAttribute("Count", Items.Count())
                , Items.Select((val, ind) => new XElement("Item",
                new object[] {
                                new XAttribute("index", ind)
                                , new XElement("Description", val.Description)
                                , new XElement("RoomID", val.Location)
                                , val.Word != null ? new XElement("Word", val.Word) :  null
                }))));

            gameData.Add(
                new XElement("Footer"
                        , new XElement("Version", Footer.Version)
                        , new XElement("AdventureNumber", Footer.AdventureNumber)
                        , new XElement("Unknown", Footer.Unknown)
                    )
                );

            gameData.Save(GameName + "commented.xml");

            return gameData;
        }

        static string[] conditions = {"item arg carried"
                        ,"item arg in room with player"
                        ,"item arg carried or in room with player"
                        ,"player in room arg"//3
                        ,"item arg not in room with player"//4
                        ,"item arg not carried" //5
                        ,"player not it room arg"   //6
                        ,"bitflag arg is set"
                        ,"bitflag arg is false"
                        ,"something carried"
                        ,"nothing carried"
                        ,"item arg not carried or in room with player"//11
                        ,"item arg in game"//12
                        ,"item arg not in game"//13
                        ,"current counter less than arg"//14
                        ,"current counter greater than arg"//15
                        ,"object arg in initial location" //16
                        ,"object arg not in initial location"//17
                        ,"current counter equals arg"};

        int[] conditionsWithItems = { 0, 1, 2, 5, 11, 12, 13, 16, 17 };


        static string[] actions =
            { "get item ARG1, check if can carry"   //52
                ,"drops item ARG1 into current room"    //53
                ,"move room ARG1"       //54
                ,"Item ARG1 is removed from the game (put in room 0)"   //55
                ,"set darkness flag"
                ,"clear darkness flag"
                ,"set ARG1 flag"    //58
                ,"Item ARG1 is removed from the game (put in room 0)"//59
                ,"set ARG1 flag"   //60
                ,"Death, clear dark flag, move to last room"
                ,"item ARG1 is moved to room ARG2"  //62
                ,"game over"
                ,"look"
                ,"score"//65
                ," output inventory"
                ,"Set bit 0 true"
                ,"Set bit 0 false"
                ,"refill lamp"
                ,"clear screen"
                ,"save game"
                ,"swap item locations ARG1 ARG2"//72
                ,"continue with next action"
                ,"take item ARG1, no check done to see if can carry"//74
                ,"put item 1 ARG1 with item2 ARG2"//75
                ,"look"
                ,"decement current counter"//77
                ,"output current counter"//77
                ,"set current counter value arg1"
                ,"swap location with saved location"
                ,"Select counter arg1. Current counter is swapped with backup counter"//80
                ,"add to current counter"//81
                ,"subtract from current counter"//82
                ,"echo noun without cr"//83
                ,"echo noun"
                ,"Carriage Return"//85
                ,"Swap current location value with backup location-swap value"//86
                ,"wait 2 seconds"};

        static int[] twoArgActions = { 62, 72, 75 };
        static int[] oneActionArgs = { 52, 53, 54, 55, 58, 59, 60, 74, 78, 81, 82, 83, 79 };


        static int[] actionArgsWithOneItem = { 52, 53, 55, 59, 74, 62 }; //note 62, a two arg action which moves item arg1 to room arg2
        static int[] actionsWithTwoItems = { 72, 75 };

        /// <summary>
        /// Build an XML element for the provided action in a big, messy statement which I had a lot of fun writing ;)
        /// </summary>
        /// <param name="pAction"></param>
        /// <param name="pIndex"></param>
        /// <param name="pIsChild"></param>
        /// <returns></returns>
        private XElement MakeAction(Action pAction, int pIndex, bool pIsChild)
        {
            return new XElement(pIsChild ? "ChildAction" : "Action"
                    , new object[]
                    {
                        new XAttribute("index", pIndex)
                        , pIsChild  || pAction.Children == null ? null:  new XAttribute("HasChildren", pAction.Children != null)
                        , pAction.Verb == 0 ? new XAttribute("Auto", pAction.Noun) : new XAttribute("Input", string.Format("{0} {1}", Verbs[pAction.Verb], Nouns[pAction.Noun]))
                        , new XElement("Verb", pAction.Verb)
                        , new XElement("Noun", pAction.Noun)
                        ,
                            !string.IsNullOrWhiteSpace(pAction.Comment)  //comments may be stored in the DAT file, so used those preferentially
                            ? new XElement("Comment", pAction.Comment)
                            : null

                        , new XElement("Conditions"
                            , pAction.Conditions.Where(con => con[0] > 0)
                                .Select(con=>
                                    new XElement("Condition"
                                    , new XElement("ConditionID", con[0]-1)
                                    , new XElement("Description", conditions[con[0]-1])
                                    , new XElement("arg", con[1] )
                                    , conditionsWithItems.Contains(con[0]-1) ? new XElement("Arg1Item", Items[con[1]].Description) : null
                                    )
                        ))

                        , new XElement("ActionComponents"
                            , pAction.Actions.Where(act => act[0] > 0)
                                .Select(act=> new XElement("Action"
                                    , new XElement("Argument", act[0])
                                    , new XElement("Description",

                                            //this beast of a statement determines how the description of the
                                            //action is generated
                                            (act[0] > 0 & act[0] < 52)
                                                ? String.Format("A Output message: {0}", replaceChars(Messages[act[0]], new char[] {'\r','\n' }))//output message
                                                : act[0] > 101
                                                    ? String.Format("A Output message: {0}", replaceChars(Messages[act[0]-50], new char[] {'\r','\n' }))    //output message
                                                    //vvv Output a string based on the number of args the action has
                                                    : oneActionArgs.Contains(act[0])
                                                        ? string.Format("{0}: {1}", actions[act[0] - 52], act[1])
                                                        : twoArgActions.Contains(act[0])
                                                            ? string.Format("{0}: {1} {2}", actions[act[0] - 52], act[1], act[2])
                                                            : string.Format("{0}", actions[act[0] - 52])



                                    )
                                    , act[1] > 0 ? new XElement("arg1", act[1] ): null
                                    , act[2] > 0 ? new XElement("arg2", act[2] ): null

                                    //if a two item argument output the item descriptions
                                    , actionsWithTwoItems.Contains(act[0]) ? new object[] {new XElement("Arg1Item", Items[act[1]].Description), new XElement("Arg2Item", Items[act[2]].Description) } : null

                                    //if a one item argument that uses items output the item description
                                    , actionArgsWithOneItem.Contains(act[0]) ? new XElement("Arg1Item", Items[act[1]].Description) : null

                                    )

                                    )
                        )
                                                 , pAction.Children != null ?
                            pAction.Children.Select((val,ind) => MakeAction(val,ind, true))
                            : null
                    }
                );
        }

        private string replaceChars(string pInput, char[] pReplace)
        {
            foreach (char r in pReplace)
                pInput = pInput.Replace(r, ' ');
            return pInput;
        }
    }
}
