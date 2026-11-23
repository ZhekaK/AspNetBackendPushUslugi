import assert from 'node:assert/strict';
import { test } from 'node:test';
import { drawColumns, width, winningLines } from '../PushPelmesh.Api/wwwroot/js/xoxi-board.js';

test('the final piece can complete two lines, including a row of five', () => {
  const board = Array.from({ length: width }, () => []);
  for (let column = 1; column <= 5; column++) board[column][2] = 1;
  board[1][0] = board[2][1] = board[4][3] = 1;
  const lines = winningLines(board, 3, 2, 1);
  assert.deepEqual(lines, [
    { start: [1, 2], end: [5, 2] },
    { start: [1, 0], end: [4, 3] }
  ]);
  const grid = {};
  drawColumns(grid, board, { canMove: false, lines, animate: true });
  assert.match(grid.innerHTML, /class="xoxi-win-lines animate"/);
  assert.equal(grid.innerHTML.match(/<line /g).length, 2);
  assert.match(grid.innerHTML, /x1="1.5" y1="4.5"\s+x2="4.5" y2="1.5"/);
});

test('detects a descending diagonal and excludes gapped runs', () => {
  const board = Array.from({ length: width }, () => []);
  for (let column = 0; column < 4; column++) board[column][3 - column] = 2;
  assert.deepEqual(winningLines(board, 3, 0, 2), [{ start: [0, 3], end: [3, 0] }]);
  delete board[1][2];
  assert.deepEqual(winningLines(board, 3, 0, 2), []);
});
