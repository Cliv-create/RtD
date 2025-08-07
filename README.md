# RtD :card_file_box:

[Читать на русском](README_RU.md)

# Why?

In your list on Shikimori you can add comments to the title. Currently, the character limit is 4067.  
So, this project exists so you can add your own comments to the anime title without needing to post them somewhere.  
Just launch the .exe once in a while to fetch the information about the titles. You can search for specific genres and read title description on Shikimori (more features might be added in the future).

# How does it work?

- You launch `.exe`
- `.exe` uses `rtd_config.json` file for all of the configuration, or  
You enter target folder, where all of the .md files will be put at.  
You enter Shikimori userId.  
- After that, DB is loaded and API request(s) performed.
- All of the titles in the list are added in the destination folder.
- The file structure goes like this:
  

> :open_file_folder: Root folder
> > :open_file_folder: "Title Name" folder
> > > :page_facing_up: "Title Name.md" file
> 
> > :open_file_folder: `Manga` folder
> > > :open_file_folder: `manga / manhwa / manhua / light_novel / novel / one_shot / doujin` folders
> > > > "Title Name.md" file
  
  
:page_facing_up: Markdown file content:
```
---
YAML section
---

# Review
text that gets fetched from your anime list (you can add text comment to the titles in your anime list)

PrivateMarker
your comments (from PrivateMarker to the end of the file)
```
Private marker exists so when you re-fetch the list your comments dont get removed.

### :page_with_curl: rtd_config.json

You can add optional configuration file, so you wouldn't have to input any information to get program working.  
rtd_config.json should be placed in the same folder as the RtD.exe  

rtd_config.json example contents:
```json
{
    "AutomaticAppExecution": true,
    "RootPath": "C:\\Anime",
    "UserId": "0000000"
}
```

- AutomaticAppExecution - set to "true" if you want program to use config file. Set to false if you want to be prompted for input every time you launch the program.
- RootPath - folder that will contain generated Markdown files.
- UserId - Your Shikimori user id.

### :floppy_disk: anime_cache.db
  
`anime_cache.db` file is created upon program launch (if none was present).  
This file will contain cached values for `title_id`, `updatedAt` and `folder_name`.
  
> [!CAUTION]
> `anime_cache.db` is very important! Make sure you don't lose it!
  
# Build info

Built using .NET Core 9 and compiled in Visual Studio 2022 (v17+)

## How to compile?

- Clone the repository.
- Download .NET Framework package for Visual Studio (see RtD.csproj), or use any other tool of your choise.
- Compile and run the code.

### Additional info

`fd` and `sd` releases  
`fd` - Framework Dependent. Smaller size, but needs `.NET Framework` installed on PC.  
`sd` - Self Contained. Larger size, but doesn't need `.NET Framework` installed on PC.  

File operations and API calls are done in batches of 50 (see limit value). This is because a new API call is performed and current API "batch size limit" is 50 titles at a time.  
Note, above is applied when re-fetching your entire anime list, or when you didn't launch program for a while, so there's more then 50 changes to your anime list. Also, it's applied when there's no `anime_cache.db` present near `RtD.exe`.  
  
Upon transferring files, it is recommended that with `RtD.exe` you also move `rtd_config.json` and, **more importantly**, `anime_cache.db`. As without these files you will not only be prompted for `folder path` and `Shikimori user id`, but you will also need to re-fetch all of your list again throught API calls.