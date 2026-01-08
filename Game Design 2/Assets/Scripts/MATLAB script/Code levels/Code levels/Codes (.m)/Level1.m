clear; clc;


% Nodes coordinates 
coords_player = [];   


% Elements (node i, node j)
elems_player = [2 3];

[Level, Player, Forces, Percentages, Deformation] = Function1(coords_player, elems_player);
