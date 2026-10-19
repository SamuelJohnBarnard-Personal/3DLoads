## How to Run

Prerequisites: Node.js/npm and the .NET 8 SDK.

From the Angular project directory, run:

```powershell
cd src/3DLoads.Web
npm install
npm start
```

This starts Angular at `http://localhost:4202` and the local C# solve host at port `5195`. Angular proxies solve requests to the C# host. Stop the command with Ctrl+C;
