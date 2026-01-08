clear; clc;


% Nodes coordinates 
coords_player = [ ];   


% Elements (node i, node j)
elems_player = [3 6
                3 5];

[Level, Player, Forces, Percentages, Deformation] = Function3(coords_player, elems_player);
