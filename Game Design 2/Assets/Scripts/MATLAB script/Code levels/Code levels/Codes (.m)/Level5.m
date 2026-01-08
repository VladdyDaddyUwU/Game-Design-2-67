clear; clc;


% Nodes coordinates 
coords_player = [1 2];   


% Elements (node i, node j)
elems_player = [3 4
                2 4
                2 5
                3 5
                4 5];

[Level, Player, Forces, Percentages, Deformation] = Function5(coords_player, elems_player);
