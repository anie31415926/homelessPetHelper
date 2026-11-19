export function routeLength(lane) {
    return lane.points.slice(1).reduce((sum, point, i) => sum + Math.hypot(point[0] - lane.points[i][0], point[1] - lane.points[i][1]), 0);
}
export function pointOnRoute(lane, distance) {
    let remaining = Math.max(0, distance);
    for (let i = 1; i < lane.points.length; i++) {
        const from = lane.points[i - 1], to = lane.points[i];
        const length = Math.hypot(to[0] - from[0], to[1] - from[1]);
        if (remaining <= length) {
            const ratio = remaining / length;
            return { x: from[0] + (to[0] - from[0]) * ratio, y: from[1] + (to[1] - from[1]) * ratio };
        }
        remaining -= length;
    }
    const end = lane.points.at(-1);
    return { x: end[0], y: end[1] };
}
export function projectOntoRoute(lane, point) {
    let best = { distance: 0, separation: Infinity }, walked = 0;
    for (let i = 1; i < lane.points.length; i++) {
        const [ax, ay] = lane.points[i - 1], [bx, by] = lane.points[i];
        const dx = bx - ax, dy = by - ay, length = Math.hypot(dx, dy);
        const ratio = Math.max(0, Math.min(1, ((point.x - ax) * dx + (point.y - ay) * dy) / (length * length)));
        const separation = Math.hypot(point.x - ax - dx * ratio, point.y - ay - dy * ratio);
        if (separation < best.separation) best = { distance: walked + length * ratio, separation };
        walked += length;
    }
    return best.distance;
}
export function positionOf(catalog, unit) {
    if (unit.transit) {
        const fraction = 1 - unit.transit.remainingMs / catalog.rules.rerouteDurationMs;
        return { x: unit.transit.from.x + (unit.transit.to.x - unit.transit.from.x) * fraction,
            y: unit.transit.from.y + (unit.transit.to.y - unit.transit.from.y) * fraction };
    }
    return pointOnRoute(catalog.map.lanes.find(lane => lane.id === unit.laneId), unit.distance);
}
export const separation = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);

export function sweepArea(catalog, laneId) {
    const lane = catalog.map.lanes.find(item => item.id === laneId);
    return { ...pointOnRoute(lane, routeLength(lane) * catalog.rules.sweepCenterAt), radius: catalog.rules.sweepRadius };
}
export const unitInArea = (catalog, unit, area) => separation(positionOf(catalog, unit), area) <= area.radius;
