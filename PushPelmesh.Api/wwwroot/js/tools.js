import { decimal, errorLine, onSubmit, title } from './ui.js';
export { xoxi } from './xoxi.js';

export function bill({ main }) {
  main.innerHTML = title('Калькулятор счёта', 'Разделите общую сумму, если один из участников платит половину.') + `<div class="split">
    <form id="bill" class="panel stack"><div class="fields"><label>Человек<input name="people" type="number" min="1" step="1" value="5" required></label>
      <label>Сумма, ₽<input name="total" type="number" min="0" step=".01" value="4500" required></label></div>
      <div class="actions"><button class="btn" type="submit">Рассчитать</button><button class="btn secondary" type="button" id="clear">Очистить</button></div>${errorLine}</form>
    <div class="panel stack" id="bill-result" aria-live="polite"></div></div>`;
  const form = main.querySelector('#bill');
  const output = main.querySelector('#bill-result');
  const money = value => new Intl.NumberFormat('ru-RU', { style: 'currency', currency: 'RUB' }).format(value);
  const recalculate = data => {
    const people = Number(data.get('people'));
    const total = Number(data.get('total'));
    const regular = total / (people - .5);
    output.innerHTML = `<h2>Доли участников</h2>
      ${people > 1 ? `<div class="row"><span>Стандартная доля · ${people - 1}</span><strong>${money(regular)}</strong></div>` : ''}
      <div class="row"><span>Льготная доля</span><strong>${money(regular / 2)}</strong></div>
      <div class="row"><span>Общая сумма</span><strong>${money(total)}</strong></div>`;
  };
  form.querySelector('#clear').onclick = () => {
    form.elements.people.value = '';
    form.elements.total.value = '';
    output.innerHTML = '<p class="muted">Введите число участников и сумму.</p>';
  };
  form.oninput = () => { if (form.checkValidity()) recalculate(new FormData(form)); };
  onSubmit(form, recalculate);
  recalculate(new FormData(form));
}

export function alcohol({ main, user }) {
  if (!(user.weightKg > 0)) {
    main.innerHTML = title('Алкометр') + '<section class="panel stack"><h2>Введите вес в профиле</h2><div><a class="btn" href="#profile">Открыть профиль</a></div></section>';
    return;
  }

  const beverages = [
    ['beer', 'Пиво', 4.5], ['vodka', 'Водка', 40], ['wine', 'Вино', 15],
    ['white-wine', 'Белое вино', 7], ['absinthe', 'Абсент', 70], ['champagne', 'Шампанское', 12],
    ['liqueur', 'Ликёр', 20], ['other', 'Другой напиток', null]
  ];
  const drinks = [];
  let hours;
  main.innerHTML = title('Алкометр', 'Условный показатель по формуле из приложения.') +
    '<section id="drink-step" class="panel stack drink-step" aria-labelledby="drink-heading"></section>';
  const stage = main.querySelector('#drink-step');
  const image = key => `<img src="/assets/drinks/${key}.png" alt="" width="75" height="75">`;
  const render = (number, heading, html, focus = true) => {
    stage.innerHTML = `<p class="meta">${number ? `Шаг ${number} из 4` : ''}</p>
      <h2 id="drink-heading" tabindex="-1">${heading}</h2>${html}`;
    if (focus) stage.querySelector('h2').focus({ preventScroll: true });
  };

  function choose(focus) {
    render(1, `Выберите напиток № ${drinks.length + 1}`, `<div class="drink-options">
      ${beverages.map(([key, name, strength], index) => `<button type="button" data-beverage="${index}">${image(key)}
        <strong>${name}</strong>${strength === null ? '' : `<small>${decimal(strength)}%</small>`}</button>`).join('')}
      </div>${drinks.length ? '<div><button class="btn secondary" id="drink-back">К напиткам</button></div>' : ''}`, focus);
    stage.querySelectorAll('[data-beverage]').forEach(button => {
      button.onclick = () => amount(beverages[Number(button.dataset.beverage)]);
    });
    const back = stage.querySelector('#drink-back');
    if (back) back.onclick = review;
  }

  function amount([key, name, strength]) {
    render(2, 'Укажите объём', `<div class="drink-selected">${image(key)}<strong>${name}</strong>
      ${strength === null ? '' : `<span class="badge">${decimal(strength)}%</span>`}</div>
      <form class="stack"><label>Объём, мл<input name="volume" type="number" min="1" step="any" required></label>
      ${strength === null ? '<label>Крепость, %<input name="strength" type="number" min=".01" max="100" step="any" required></label>' : ''}
      <div class="actions"><button class="btn" type="submit">Продолжить</button>
        <button class="btn secondary" type="button" id="drink-back">Назад</button></div>${errorLine}</form>`);
    stage.querySelector('#drink-back').onclick = () => choose(true);
    onSubmit(stage.querySelector('form'), data => {
      drinks.push({ key, name, strength: strength ?? Number(data.get('strength')), volume: Number(data.get('volume')) });
      if (hours === undefined) time();
      else review();
    });
  }

  function time() {
    render(3, 'Сколько времени прошло?', `<form class="stack">
      <label>Часы после первого напитка<input type="number" name="hours" min=".01" step="any" value="${hours ?? ''}" required></label>
      <div><button class="btn" type="submit">Продолжить</button></div>${errorLine}
    </form>`);
    onSubmit(stage.querySelector('form'), data => {
      hours = Number(data.get('hours'));
      review();
    });
  }

  function review() {
    render(4, 'Проверьте данные', `<ul class="drink-summary">${drinks.map(drink => `<li>${image(drink.key)}<span>${drink.name}</span>
      <span class="meta">${decimal(drink.volume)} мл · ${decimal(drink.strength)}%</span></li>`).join('')}</ul>
      <p class="meta">Прошло ${decimal(hours)} ч · Вес из профиля ${decimal(user.weightKg)} кг</p>
      <div class="actions"><button class="btn" type="button" id="drink-result">Рассчитать</button>
        ${drinks.length < 5 ? '<button class="btn secondary" type="button" id="drink-add">Добавить напиток</button>' : ''}
        <button class="btn secondary" type="button" id="drink-time">Изменить время</button>
        <button class="btn secondary" type="button" id="drink-reset">Начать заново</button></div>`);
    stage.querySelector('#drink-result').onclick = result;
    stage.querySelector('#drink-time').onclick = time;
    stage.querySelector('#drink-reset').onclick = reset;
    const add = stage.querySelector('#drink-add');
    if (add) add.onclick = () => choose(true);
  }

  function result() {
    const sum = drinks.reduce((total, drink) => total + drink.strength * drink.volume, 0);
    render(null, 'Результат', `<p class="drink-result">${decimal(sum / (user.weightKg * 100 * hours))}</p>
      <p class="meta">Показатель из игры. Для оценки концентрации алкоголя нужны измерения.</p>
      <div><button class="btn secondary" type="button" id="drink-reset">Начать заново</button></div>`);
    stage.querySelector('#drink-reset').onclick = reset;
  }

  function reset() {
    drinks.length = 0;
    hours = undefined;
    choose(true);
  }

  choose(false);
}
