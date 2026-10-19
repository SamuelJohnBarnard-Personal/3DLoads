## What is this project?

This is a project that optimises the arrangement of packages of various different sizes into a container, using a non-deterministic optimisation technique. This is a solver for a 3D variant of the bin packing problem: https://en.wikipedia.org/wiki/Bin_packing_problem

I have built this project with an Angular frontend and a C# backend.

## How it works/what this solves

Consider you have a van, and want to deliver a number of large, cuboid packages, which have a wide variety of sizes. A driver might want to arrange said packages inside his van in such a way that he makes maximum use of the available space.

As the number of packages increases, this problem becomes increasingly difficult to solve. A variety of optimisation techniques have been developed and discussed to approach this problem. This project implements one of them, a type of genetic algorithm known as BRKGA (Biased Random Key Genetic Algorithm - see more here - https://www.sciencedirect.com/science/article/pii/S0377221724002303), which is among the most modern approaches to solving the 3D bin packing problem.

The solver considers a variety of things. For example, packages that are to be delivered earlier in the journey, need to be located in front of packages that are to be delivered later in the journey. Some packages may allow stacking of other packages on top of them, but only up to a certain weight. And while some packages can be rotated to any orientation, other packages must have a particular side face up.

## How to Run

Prerequisites: Node.js/npm and the .NET 8 SDK.

From the Angular project directory, run:

```powershell
cd src/3DLoads.Web
npm install
npm start
```

This starts Angular at `http://localhost:4202` and the local C# solve host at port `5195`. Angular proxies solve requests to the C# host. Stop the command with Ctrl+C;
