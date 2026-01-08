function [Level, Player, Forces, Percentages, Deformation] = Function6(coords_player, elems_player)

%level-dependent values
level = 6;
Available_material = 20;
txt = "Try to support the load-bearing node using as little material as possible! The less material you use, the more stars you'll get when you beat the level."; 

Mass = 20000; 
side_width = 0.05;


coords_given = [0 0 
                1 0
                2 2
                0 1
                1 1
                ];   

elems_given = [1 4   
                5 4  
                2 5    
                ];

x_dead = [1 3];
y_dead = [0 2];

level_height = 3;
level_width = 3;
area = [0 level_width;0 level_height];


%level plot
Level = figure;
dead_zone = polyshape([x_dead(1) x_dead(2) x_dead(2) x_dead(1)],[y_dead(1) y_dead(1) y_dead(2) y_dead(2)]);
plot(dead_zone,'EdgeColor','none','FaceColor','red','FaceAlpha',0.1)
hold on
axis equal
grid on
scatter(coords_given(1:2,1),coords_given(1:2,2),150,'filled','^',"black",'LineWidth',1.5);
scatter(coords_given(3,1),coords_given(3,2),150,'filled','square',"black",'LineWidth',1.5);
scatter(coords_given(4:height(coords_given),1),coords_given(4:height(coords_given),2),'filled',"black");
xlim(area(1,:))
ylim(area(2,:))
set(gca,'xtick',0:1:level_width)
set(gca,'ytick',0:1:level_height)
for i = 1:height(elems_given)
    plot([coords_given(elems_given(i,1),1), coords_given(elems_given(i,2),1)], ...
        [coords_given(elems_given(i,1),2), coords_given(elems_given(i,2),2)],"black")
end  
quiver(coords_given(3,1), coords_given(3,2)+0.665, 0, -0.7, 'linewidth',2,'MaxHeadSize',1,'Color',[0.467 0.675 0.188]);
text(coords_given(3,1),coords_given(3,2)+0.75,sprintf('M = %d kg', Mass),'BackgroundColor', 'w', ...
        'HorizontalAlignment', 'center','Color',[0.467 0.675 0.188],'FontSize',14);
hold off
title(sprintf('Level %d',level),'Fontsize',12)
fprintf('Available material = %.1f m\n\n', Available_material)
txt2 = textwrap(string(txt), 70);
txt3 = strjoin(txt2, newline);
disp(txt3)

%making matrices
coords = zeros(height(coords_given)+height(coords_player),2);
coords(1:height(coords_given),:) = coords_given;
coords((height(coords_given)+1):(height(coords_given)+height(coords_player)),:) = coords_player;

elems = zeros(height(elems_given)+height(elems_player),2);
elems(1:height(elems_given),:) = elems_given;
elems((height(elems_given)+1):(height(elems_given)+height(elems_player)),:) = elems_player;

numNodes = height(coords);
numElems = height(elems);

% Boundary conditions:
%   Node 1: pinned → fix x,y → DOF 1,2
%   Node 2: pinned → fix x,y → DOF 1,2
fixedDOF = [1 2 3 4];
freeDOF  = setdiff(1:2*numNodes, fixedDOF);

% Load:
%   Node 3 (2,1) downward → DOF 2*3 = 6
F = zeros(2*numNodes,1);
g = 9.81;
F(6) = -g*Mass;

% Material properties
E = 210e9;          % Young's modulus (Pa)
A = side_width^2;   % Cross-sectional area (m^2) 
sigma = 250e6;      % Yield stress

% Global stiffness matrix
K = zeros(2*numNodes);
L = zeros(numElems,1);

for e = 1:numElems
    ni = elems(e,1);
    nj = elems(e,2);

    xi = coords(ni,1); yi = coords(ni,2);
    xj = coords(nj,1); yj = coords(nj,2);

    L(e) = sqrt((xj - xi)^2 + (yj - yi)^2);
    cx = (xj - xi)/L(e);
    cy = (yj - yi)/L(e);

    ke = (A*E/L(e)) * [ cx*cx   cx*cy  -cx*cx  -cx*cy;
                     cx*cy   cy*cy  -cx*cy  -cy*cy;
                    -cx*cx  -cx*cy   cx*cx   cx*cy;
                    -cx*cy  -cy*cy   cx*cy   cy*cy ];

    dof = [2*ni-1  2*ni  2*nj-1  2*nj];
    K(dof,dof) = K(dof,dof) + ke;
end

% Solve displacements
u = zeros(2*numNodes,1);
u(freeDOF) = K(freeDOF,freeDOF) \ F(freeDOF);

% Member forces
memberForces = zeros(numElems,1);
for e = 1:numElems
    ni = elems(e,1);
    nj = elems(e,2);

    xi = coords(ni,1); yi = coords(ni,2);
    xj = coords(nj,1); yj = coords(nj,2);

    cx = (xj - xi)/L(e);
    cy = (yj - yi)/L(e);

    ue = u([2*ni-1 2*ni 2*nj-1 2*nj]);
    memberForces(e) = (A*E/L(e)) * [-cx -cy cx cy] * ue;
end

% Rank / nullity
Kff = K(freeDOF, freeDOF);
r = rank(Kff);
n = length(freeDOF);
nullity = n - r;

%Angle of elements
rotation = zeros(numElems);
for i = 1:numElems
    if coords(elems(i,1),1) == coords(elems(i,2),1)
        rotation(i) = 90;
    else
    rotation(i) = atan((coords(elems(i,1),2) - coords(elems(i,2),2))/ ...
        (coords(elems(i,1),1) - coords(elems(i,2),1)))/pi*180;
    end
end    

%Percentages (compressive vs tensile vs zero-force members)
I = (side_width^4)/12;
K = 1;
P = zeros(numElems,1);
percentages = zeros(numElems,1);
for i = 1:numElems
    P(i) = (pi^2 * E * I)/(K^2*L(i)^2); %Euler critical load (compression)
    Fmax = A * sigma; %tension
    if abs(memberForces(i)) < 0.0001
    percentages(i) = 0;
    elseif memberForces(i) > 0
    percentages(i) = memberForces(i)/Fmax * 100;
    else 
    percentages(i) = max(-memberForces(i)/P(i) * 100, -memberForces(i)/Fmax * 100);
    end
end    

%Displaced coordinates
coords_disp = zeros(size(coords));
mag = abs(coords(1,2)-coords(3,2));
scale = 0.2*mag/max(abs(u));
for i=1:numNodes
    coords_disp(i,1) = coords(i,1) + scale*u(2*i-1);
    coords_disp(i,2) = coords(i,2) + scale*u(2*i);
end

%player plot
Player = figure;
plot(dead_zone,'EdgeColor','none','FaceColor','red','FaceAlpha',0.1)
hold on
axis equal
grid on
scatter(coords(1:2,1),coords(1:2,2),150,'filled','^',"black",'LineWidth',1.5);
scatter(coords(3,1),coords(3,2),150,'filled','square',"black",'LineWidth',1.5);
scatter(coords_given(4:height(coords_given),1),coords_given(4:height(coords_given),2),'filled',"black");
%scatter(coords_player(1:height(coords_player),1),coords_player(1:height(coords_player),2),'filled',"blue");
xlim(area(1,:))
ylim(area(2,:))
set(gca,'xtick',0:1:level_width)
set(gca,'ytick',0:1:level_height)
for i = 1:height(elems_given)
    plot([coords(elems_given(i,1),1), coords(elems_given(i,2),1)], ...
        [coords(elems_given(i,1),2), coords(elems_given(i,2),2)],"black")
end
for i = 1:height(elems_player)
    plot([coords(elems_player(i,1),1), coords(elems_player(i,2),1)], ...
        [coords(elems_player(i,1),2), coords(elems_player(i,2),2)],"blue")
end   
quiver(coords(3,1), coords(3,2)+0.665, 0, -0.7, 'linewidth',2,'MaxHeadSize',1,'Color',[0.467 0.675 0.188]);
text(coords(3,1),coords(3,2)+0.75,sprintf('M = %d kg', Mass),'BackgroundColor', 'w', ...
        'HorizontalAlignment', 'center','Color',[0.467 0.675 0.188],'FontSize',14);
hold off
title('Player input members','Fontsize',12)

player_length = sum(L(height(elems_given)+1:height(elems)));
fprintf('Length of members created by player = %.2f m\n', player_length)

fprintf('\nKff rank = %d, free DOFs = %d, nullity = %d\n', r, n, nullity);
if nullity>0
    fprintf('Mechanism or near-mechanism detected.\n');
end
if nullity == 0
    fprintf('No mechanism detected.\n');
end

fprintf('\nLargest tensile force in members = %.1f N', max(memberForces));
fprintf('\nLargest compressive force in members = %.1f N\n', min(memberForces));

fprintf('\nmax percentage = %.1f%%\n',max(percentages));

if max(percentages) <100 && nullity == 0
    fprintf('\nThe structure holds')
end
if max(percentages) >=100 || nullity > 0
    fprintf('\nThe structure does not hold')
end


%forces plot
Forces = figure;
hold on
axis equal
grid on
scatter(coords(1:2,1),coords(1:2,2),150,'filled','^',"black",'LineWidth',1.5);
scatter(coords(3,1),coords(3,2),150,'filled','square',"black",'LineWidth',1.5);
scatter(coords(4:numNodes,1),coords(4:numNodes,2),'filled',"black");
xlim(area(1,:))
ylim(area(2,:)+0.1)
set(gca,'xtick',0:1:level_width)
set(gca,'ytick',0:1:level_height)
for i = 1:numElems
    plot([coords(elems(i,1),1), coords(elems(i,2),1)], ...
        [coords(elems(i,1),2), coords(elems(i,2),2)],"blue")
    text(abs(2*coords(elems(i,1),1) + coords(elems(i,2),1))/3, ...
        abs(2*coords(elems(i,1),2) + coords(elems(i,2),2))/3, ...
        string(round(memberForces(i),0)), 'BackgroundColor', 'w', ...
        'HorizontalAlignment', 'center','rotation', rotation(i));
end    
quiver(coords(3,1), coords(3,2)+0.665, 0, -0.7, 'linewidth',2,'MaxHeadSize',1,'Color',[0.467 0.675 0.188]);
text(coords(3,1),coords(3,2)+0.75,sprintf('M = %d kg', Mass),'BackgroundColor', 'w', ...
        'HorizontalAlignment', 'center','Color',[0.467 0.675 0.188],'FontSize',14);
hold off
title('Forces on members (N)','Fontsize',12)

%Percentages plot
Percentages = figure;
scatter(coords(1:numNodes,1),coords(1:numNodes,2),'filled',"black");
axis equal
hold on
grid on
xlim(area(1,:))
ylim(area(2,:)+0.1)
set(gca,'xtick',0:1:level_width)
set(gca,'ytick',0:1:level_height)
for i = 1:numElems
    if abs(memberForces(i)) < 0.0001
    plot([coords(elems(i,1),1), coords(elems(i,2),1)], ...
        [coords(elems(i,1),2), coords(elems(i,2),2)],"black")
    elseif memberForces(i) > 0
         plot([coords(elems(i,1),1), coords(elems(i,2),1)], ...
        [coords(elems(i,1),2), coords(elems(i,2),2)],"blue")
    else 
         plot([coords(elems(i,1),1), coords(elems(i,2),1)], ...
        [coords(elems(i,1),2), coords(elems(i,2),2)],"red")
    end
    text(abs(2*coords(elems(i,1),1) + coords(elems(i,2),1))/3, ...
        abs(2*coords(elems(i,1),2) + coords(elems(i,2),2))/3, ...
        sprintf('%d %%',round(percentages(i))), 'BackgroundColor', 'w', ...
        'HorizontalAlignment', 'center','rotation', rotation(i));
end    
quiver(coords(3,1), coords(3,2)+0.665, 0, -0.7, 'linewidth',2,'MaxHeadSize',1,'Color',[0.467 0.675 0.188]);
text(coords(3,1),coords(3,2)+0.75,sprintf('M = %d kg', Mass),'BackgroundColor', 'w', ...
        'HorizontalAlignment', 'center','Color',[0.467 0.675 0.188],'FontSize',14);
legend = text([1.5 1.5],[0.5+0.04*level_height 0.5-0.04*level_height],{'blue = tension' 'red = compression'},...
    'BackgroundColor', 'w','HorizontalAlignment', 'left','FontSize',12);
legend(1).Color = 'blue'; 
legend(2).Color = 'red'; 
hold off
title('Percentage of critical/buckling load','Fontsize',12)

%deformation plot
Deformation = figure;
scatter(coords_disp(1:numNodes,1),coords_disp(1:numNodes,2),'filled',"red");
axis equal
hold on 
grid on
xlim([(3*min(coords_disp(:,1))) (max(max(coords_disp(:,1)),max(coords(:,1))))+0.2])
ylim(area(2,:))
set(gca,'xtick',0:1:level_width)
set(gca,'ytick',0:1:level_height)
for i = 1:numElems
    plot([coords(elems(i,1),1), coords(elems(i,2),1)], ...
        [coords(elems(i,1),2), coords(elems(i,2),2)],"blue")
    plot([coords_disp(elems(i,1),1), coords_disp(elems(i,2),1)], ...
        [coords_disp(elems(i,1),2), coords_disp(elems(i,2),2)],"red",'linewidth',1.5)
end    
quiver(coords_disp(3,1), coords_disp(3,2)+0.665, 0, -0.7, 'linewidth',2,'MaxHeadSize',1,'Color',[0.467 0.675 0.188]);
text(coords_disp(3,1),coords_disp(3,2)+0.75,sprintf('M = %d kg', Mass),'BackgroundColor', 'w', ...
        'HorizontalAlignment', 'center','Color',[0.467 0.675 0.188],'FontSize',14);
title('Deformation plot of truss (normalised displacement)','Fontsize',12)
