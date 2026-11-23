export const width = 10;
export const height = 5;
export const figure = player => player === 1 ? '×' : '○';

export function winningLines(board, column, row, player) {
  return [[1, 0], [0, 1], [1, 1], [1, -1]].flatMap(([dx, dy]) => {
    const count = direction => {
      let steps = 0;
      while (board[column + dx * (steps + 1) * direction]?.[row + dy * (steps + 1) * direction] === player)
        steps++;
      return steps;
    };
    const from = count(-1);
    const to = count(1);
    return from + to + 1 >= 4
      ? [{ start: [column - dx * from, row - dy * from], end: [column + dx * to, row + dy * to] }]
      : [];
  });
}

export function drawColumns(grid, board, { canMove, last = [], animate = false, mark = true,
  lines = [], columnLabel = 'Колонка', emptyLabel = 'пусто' }) {
  const columns = board.map((column, index) => {
    const cells = Array.from({ length: height }, (_, number) => height - number - 1).map(row => {
      const player = column[row];
      const latest = last[0] === index && last[1] === row;
      return `<span class="${latest && mark ? 'last' : ''}"><b class="${latest && animate ? 'fall' : ''}">${player ? figure(player) : ''}</b></span>`;
    });
    return `<button type="button" data-column="${index}" aria-label="${columnLabel} ${index + 1}: ${column.filter(Boolean).map(figure).join(' ') || emptyLabel}"
      ${!canMove || column[height - 1] ? 'disabled' : ''}>${cells.join('')}</button>`;
  }).join('');
  const overlay = lines.length ? `<svg class="xoxi-win-lines${animate ? ' animate' : ''}"
    viewBox="0 0 ${width} ${height}" preserveAspectRatio="none" aria-hidden="true" focusable="false">
    ${lines.map(({ start: [x1, y1], end: [x2, y2] }) => `<line x1="${x1 + .5}" y1="${height - y1 - .5}"
      x2="${x2 + .5}" y2="${height - y2 - .5}" pathLength="1" vector-effect="non-scaling-stroke" />`).join('')}
    </svg>` : '';
  grid.innerHTML = columns + overlay;
}
