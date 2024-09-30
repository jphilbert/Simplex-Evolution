# Simplex-Evolution
The demo is a front end to an optimizer used on a large multivariate model for financial trades many years ago. This was a time before we had cheap powerful computers and we called ourselves data scientists. In those young naive years, we had to take some liberties and be a bit creative in how we approached the problem mathematically and programmed it.

  * [Optimization in Real Life](#Optimization-in-Real-Life)
  * [Evolution into Amoebas](#Evolution-into-Amoebas)
  * [Improving Performance Through Threads](#Improving-Performance-Through-Threads)
  * [Overview of Application](#Overview-of-Application)

# Optimization in Real Life
So what exactly is [optimization](https://en.wikipedia.org/wiki/Mathematical_optimization)? Let's have old me explain it from some archival documentation:

> Optimization is a fancy word for a something that will find a minimum or maximum value. Usually these are mathematical devices that for a given a function will yield its minimum (though this usually requires calculus or some other novel idea). This doesn't use any of that, it is more intuitive and down-to-earth.

> Confused? Well lets back track, suppose you are here in Pittsburgh on Mt. Washington, would we be on a maximum or minimum? Well that's a loaded question since I did not say exactly where we were on the mountain but we could easily figure it out. We could simple look around in a full circle and see if there are higher or lower point. If we wanted to find a maximum and saw a place higher than us, we could simply walk there and look around again and hopefully find that we are on the maximum now. Unfortunately we used our eyes which would be analogous to a mathematician having an actual expression on hand (i.e. all the information).

> Usually we don't have "eyes" to see the maximum or minimum. This would be as if I blind folded you and asked you now to find that location. Well what you could do is take test steps in certain directions and if any of them feel higher, move in that direction, ending when all the directions you test feel lower. This is a common algorithm, however if falls into a problem that even more sophisticated methods fail to solve. This being, what if you happen to be on a little ant hill in a giant valley. Obviously you would think you were on a maximum, even though you're not (actually you are on a  local maximum, however ultimately we wish to find the global one).

> So how to fix this. Well there are several ways, however I have a novel suggestion: Recruit friends. Let us grab 10 people and place them all around Pittsburgh randomly. After they moved around blindfolded for a certain amount of time, let them communicate their positions and heights (suppose they have a GPS and cell phones). Once they did that allow them to teleport to another location of their choosing and let them proceed with moving around. Sooner or later (luckily sooner) they will converge on the global maximum and this is exactly what this algorithm does. The only dramatic difference is instead of an uninteresting landscape like Pittsburgh, the demo finds the unique minimum from hundreds of slightly larger valleys, and rather than the bland 2 dimensional example, we have 24 dimensions. The particular function used is the  [Griewank function](https://en.wikipedia.org/wiki/Griewank_function).



# Evolution into Amoebas
So that's the basics of what we are doing is the simplest terms. Of course the [hill climbing method](https://en.wikipedia.org/wiki/Hill_climbing#Problems) described is all well and good for straightforward problems but is terrible for several reasons. A more complex algorithm better suited is the [Nelder-Mead Simplex Method](http://en.wikipedia.org/wiki/Nelder-Mead_method "Wiki: Nelder-Mead method"). The common analogy of this is an amoeba composed of 3 points (in our 2D example) which can ooze and contract as needed. An important perk of this method is that it does not require the use of derivatives which either need to be known or estimated (requiring additional steps).

Unfortunately replacing all of our friends with amoebas does not solve a significant problem many algorithms which is being trapped into [local extremum](https://en.wikipedia.org/wiki/Maximum_and_minimum). Once they get stuck, we need a way for them to communicate and become free. This can done via a [Genetic Algorithm](http://en.wikipedia.org/wiki/Genetic_Algorithm "Wiki: Genetic Algorithm") which mimics how organisms evolve. Once each simplex finds a good spot, they are ranked, married, and create a new generation. Their offspring move about and repeat. 

You may wonder, why even bother with letting them move about since genetic algorithms, just in real life, mutate. This can be perceived as a little bit of random nudging. Those that were nudged in the right direction would overtime outlast those that went in the wrong direction. Indeed this does work, however allowing each generation to feel out their territory before breeding them makes the process more deterministic and easier to analyze. How long we allow them to move vs how much mutation is in the art of the tuning this algorithm. We even have choice in how we pair them up and reproduce.



# Improving Performance Through Threads
> [!NOTE]
> Old verbiage, rewrite
Many may think that this is overly complicated especially with the use of events. I would not disagree, however the particular purpose of this algorithm required it. Due to its design the algorithm stops what ever it was doing when a particular number of evaluations are required. This allows another part of the program to actually handle those evaluations when and how it chooses, only requiring to return to this algorithm when finished.

In other words, this allows the actual evaluations (the actual meat of the optimization) to be done on multiple  threads  asynchronously on separate processors. In this case, a 16 processor computer optimizing a 35 parameter model trading on a simulated stock market. One simulation (i.e. all processors requesting an evaluation) can take up to 10 minutes. Without implementing an evaluation bin and ability to multi-thread, this process would take at least 16 times as long.



# Code Overview
> [!NOTE]
> Old verbiage, rewrite
A few quick notes on the source code. Its programmed in  C#  .net 2.0  and used  [ZedGraph](http://zedgraph.org "Graphics package for .net.")  for graphing (why would I write one if someone already made a better one). The actual algorithm is self contained (in that it need only request parameter be evaluated ) and thread safe. Internally it is driven by an  message queue  which communicates between it's  classes.

The algorithm makes use of 3  classes  that are hierarchically connected. The first being a  [single simplex](Source/Simplex.cs)  which handles the daily duties of an amoeba (flopping around, stretching, contracting, and so on). Apart from being event driven, is basically a standalone  [Nelder-Mead Simplex Algorithm](http://en.wikipedia.org/wiki/Nelder-Mead_method "Wiki: Nelder-Mead method").

The next is the  [work colony](Source/SimplexWorkColony.cs)  which is a collection of simplexes doing their thing. Apart from being a simple container, it handles the evaluation bin which is a collection of parameter sets that need to be evaluated and then sent back to their respective workers. This class can be thought of as the foreman that needs to convey info between the workers and president. The bin's purpose is to keep the foreman from talking to the president about every little thing.

The last is the  [genetics](Source/SimplexGenetics.cs)  which supersedes the colony. It owns the colony and every so often stops it and forces them to evolve. At the heart of this class is simply a down and dirty  [Genetic Algorithm](http://en.wikipedia.org/wiki/Genetic_Algorithm "Wiki: Genetic Algorithm"). Its not standalone like the simplex  class  because it is specifically tailored to handle and own a colony. The reason for this is that it actually reuses the colony. It may seem biologically wrong but the worker's children are the workers themselves.

Apart from each of these  classes  having a hierarchy of  message queues  each has its own diagnostic log. This  [simple logger](Source/SimpleLogger.cs)  keeps particular info based on priority of each object and allows them to be merged and saved. It is not apparent in the  [demo](Simplex_Exe.cs)  however it is within the source code.

The last three  classes ([main](Source/Main.cs),  [settings](Source/Settings.cs), and  [best](Source/Best.cs)) are for their respective windows. The  [main](Source/Main.cs)  class  may be of interest since it contains the actual implementation of the algorithm and use of  [ZedGraph](http://zedgraph.org "Graphics package for .net."). Additionally it handles the various window events. The  [settings](Source/Settings.cs) class  contains a plethora of boring  controls  however also contain the hidden gem of simple error checking. The  [best](Source/Best.cs)  class is trivial and is only included for completeness.

# Overview of Application
> [!NOTE]
> Old verbiage, rewrite 
[This is a graphical demo](Simplex Evolution Demo.zip "Zip file of demo")  of the hybrid genetic simplex method discussed on previous pages. The zip file which should be easily uncompressed in many operating systems contains the executable and two dll's (one being the optimization library and the other  [ZedGraph](http://zedgraph.org "Graphics package for .net.")). It should be kept in mind that these files are for educational purposes only.

The main window will show the progress of each simplex (various colors) at each iteration of the simplex algorithm upon clicking run. Repeated pressing run, the simplexes will converge onto the minimum of exactly 0. This minimum can be varied but by default is at 0. Note that the graph is semi-log thus the convergence is actually quite fast. More specific details are explained on the below pictures.

Users may wish to compare this demo to a  [simple genetic algorithm on the Griewank function](http://www.aridolan.com/ga/gaa/Griewank.html).

> [!TODO]
> Add screenshots here

> [!TODO]
> Add animation
