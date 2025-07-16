# RtD

# Why?

In your anime list on Shikimori you can add comments to the title. Currently, the character limit is 4067.  
So, this project exists so you can add your own comments to the anime title without needing to post them somewhere.  
Just launch the .exe once in a while to fetch the information about the titles. You can search for specific genres and read title description on Shikimori (more features might be added in the future).

# How does it work?

- You launch .exe
- You enter target folder, where all of the .md files will be put at.
- You enter Shikimori userId.
- After that, API request is performed.
- All of the titles in the list are added in the destination folder.
- The file structure goes like this:
```
Root folder
    "Title Name" folder
        "Title Name.md" file
```

Markdown file content:
```
---
YAML section
---

# Review
(text that gets fetched from your anime list (you can add text comment to the titles in your anime list))

PrivateMarker
(your comments (from PrivateMarker to the end of the file))
```
Private marker exists so when you re-fetch the list your comments dont get removed.

## Build info

Built using .NET Core 9 and compiled in Visual Studio 2022 (v17+)

## How to compile?

- Clone the repository.
- Download .NET Framework package for Visual Studio (see Rtd.csproj), or use any other tool of your choise.
- Compile and run the code (later releases might appear).

### Additional info

Project code was written using LLM model. Then i looked throught all of the code myself.
The .exe files in releases (might appear) are a publish version with dependencies built-in, that's why the file size is so large.