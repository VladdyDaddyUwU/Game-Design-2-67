function [Forces, Percentages, Deformation] = code_function(Mass, side_width, coords, elems)

numNodes = length(coords);
numElems = length(elems);

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
fprintf('Kff rank = %d, free DOFs = %d, nullity = %d\n', r, n, nullity);
if nullity>0
    fprintf('Mechanism or near-mechanism detected.\n');
end
if nullity == 0
    fprintf('No mechanism detected.\n');
end

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
    percentages(i) = -memberForces(i)/P(i) * 100;
    end
end    

%Displaced coordinates
coords_disp = zeros(size(coords));
mag = abs(coords(1,2)-coords(3,2));
scale = 0.8*mag/max(abs(u));
for i=1:numNodes
    coords_disp(i,1) = coords(i,1) + scale*u(2*i-1);
    coords_disp(i,2) = coords(i,2) + scale*u(2*i);
end

%forces plot
Forces = figure;
scatter(coords(1:numNodes,1),coords(1:numNodes,2),'filled',"red");
axis equal
hold on
grid on
xlim([0 2.3])
ylim([0 2.1])
set(gca,'xtick',0:1:2)
set(gca,'ytick',0:1:2)
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
fprintf('Largest tensile force in members = %.1f N\n', max(memberForces));
fprintf('Largest compressive force in members = %.1f N\n', min(memberForces));

%Percentages plot
Percentages = figure;
scatter(coords(1:numNodes,1),coords(1:numNodes,2),'filled',"black");
axis equal
hold on
grid on
xlim([0 2.3])
ylim([0 2.1])
set(gca,'xtick',0:1:2)
set(gca,'ytick',0:1:2)
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
legend = text([1.5 1.5],[0.58 0.42],{'blue = tension' 'red = compression'},...
    'BackgroundColor', 'w','HorizontalAlignment', 'left','FontSize',12);
legend(1).Color = 'blue'; 
legend(2).Color = 'red'; 
hold off
title('Percentage of critical/buckling load','Fontsize',12)
fprintf('\nmax percentage = %d%%',round(max(percentages)));
if max(percentages) <100
    fprintf('\nThe structure holds')
end
if max(percentages) >=100
    fprintf('\nThe structure breaks')
end

%deformation plot
Deformation = figure;
scatter(coords_disp(1:numNodes,1),coords_disp(1:numNodes,2),'filled',"red");
axis equal
hold on 
grid on
xlim([(3*min(coords_disp(:,1))) (max(max(coords_disp(:,1)),max(coords(:,1))))+0.2])
ylim([0 2.1])
set(gca,'xtick',0:1:2)
set(gca,'ytick',0:1:2)
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
