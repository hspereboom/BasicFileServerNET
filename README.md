### About
`Ostracod` is a http file server implementation, meant for testing purposes;  
it publishes a designated directory.  

### Integration
The main package (namespace) is `BFS`; please rebrand at will.  
There are no dependencies outside OOTB .NET itself.  
The code is compatible with C# 4.0.  
The project targets framework 4.8.  

### Configuration
The config file `.ostracod` is unused at the moment and can be omitted.  

`Web.Config` contains the following keys:  
- `docFolder`  
  The root directory path to expose, which may be  
    a) Absolute  
    b) Relative to the working directory  
    c) An IIS virtual directory  
- `logFolder`  
  (unused)  

### Licensing
All code is distributed under the MIT license https://opensource.org/license/mit.  
For easy comparison with other licenses, see https://choosealicense.com/licenses.  
