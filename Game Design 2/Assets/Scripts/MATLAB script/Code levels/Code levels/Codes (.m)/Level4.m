clear; clc;


% Nodes coordinates 
coords_player = [1 2];   


% Elements (node i, node j)
elems_player = [4 6     
                6 5   
                3 5   
                3 6];

[Level, Player, Forces, Percentages, Deformation] = Function4(coords_player, elems_player);
