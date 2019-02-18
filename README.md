# ScottFreeLoader
Utilities for opening ScottFree (.dat) files

A Visual Studio C# project which produces a DLL which offers a variety of functions for doing things with ScottFree files.  

## Usage

```c#
using System;
using ScottFreeLoader;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            GameData g; //info stored here

            //load a game
            g = ScottFreeLoader.GameData.Load("somefile.dat");

            //save game as formated XML with lots of comments explaining
            //what is going on
            g.XMLOutput();
        }
    }
}

```

## GameData

GameData is a class that contains the ScottFree structures loaded into the following public properties

1. Header - a class containing 12 properties containing basic game set up properties.
2. Footer - a class containing 3 properties, not used in the game but included for completeness.
3. Rooms - game rooms, stored in an array of the Room class.
4. Actions - game actions, stored in an array of the Action class
5. Items - game items, stored in an an array of the Item class.
6. Verbs - game verbs, stored in a string array.
7. Nouns - game nouns, stored in a string array.
8. Messages - game messages, stored in a string array.
9. GameName - the name of loaded file.
