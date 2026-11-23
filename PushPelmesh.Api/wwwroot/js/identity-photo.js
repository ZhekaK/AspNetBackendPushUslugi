import { onSubmit } from './ui.js';

const photoPath = '/api/user/profile/photo';

async function preparePhoto(file) {
  if (!file?.size || file.size > 32 * 1024 * 1024)
    throw new Error('Выберите фото размером до 32 МБ.');

  const url = URL.createObjectURL(file);
  try {
    const image = new Image();
    image.src = url;
    try { await image.decode(); }
    catch { throw new Error('Браузер не смог открыть фото. Попробуйте JPEG или PNG.'); }
    const width = image.naturalWidth;
    const height = image.naturalHeight;
    const canvas = document.createElement('canvas');
    canvas.width = 480;
    canvas.height = 640;
    const cropWidth = Math.min(width, height * 3 / 4);
    const cropHeight = Math.min(height, width * 4 / 3);
    const context = canvas.getContext('2d', { alpha: false });
    context.fillStyle = '#fff';
    context.fillRect(0, 0, 480, 640);
    context.drawImage(image, (width - cropWidth) / 2, (height - cropHeight) / 2,
      cropWidth, cropHeight, 0, 0, 480, 640);
    const jpeg = await new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', .87));
    if (!jpeg || jpeg.size > 512 * 1024) throw new Error('Не удалось подготовить фото.');
    return jpeg;
  } finally {
    URL.revokeObjectURL(url);
  }
}

export function bindIdentityPhoto(dialog, request, signal, notify) {
  const form = dialog.querySelector('#identity-photo-form');
  const image = dialog.querySelector('#identity-photo');
  const remove = dialog.querySelector('#delete-identity-photo');
  let objectUrl;
  let revision = 0;

  function showPhoto(blob) {
    if (signal.aborted) return;
    if (objectUrl) URL.revokeObjectURL(objectUrl);
    objectUrl = blob ? URL.createObjectURL(blob) : undefined;
    if (objectUrl) image.src = objectUrl;
    else image.removeAttribute('src');
    image.hidden = !objectUrl;
    remove.hidden = !objectUrl;
  }

  signal.addEventListener('abort', () => {
    if (objectUrl) URL.revokeObjectURL(objectUrl);
  }, { once: true });

  void request(photoPath, { responseType: 'blob' }).then(blob => {
    if (revision === 0) showPhoto(blob);
  }).catch(error => {
    if (!signal.aborted && error.status !== 404) notify(error.message);
  });

  onSubmit(form, async data => {
    const jpeg = await preparePhoto(data.get('photo'));
    revision++;
    remove.disabled = true;
    try {
      await request(photoPath, { method: 'PUT', body: jpeg });
      showPhoto(jpeg);
      form.reset();
      notify('Фотография сохранена.');
    } finally {
      remove.disabled = false;
    }
  });

  remove.onclick = async () => {
    if (form.dataset.busy) return;
    const submit = form.querySelector('[type=submit]');
    revision++;
    remove.disabled = submit.disabled = true;
    try {
      await request(photoPath, { method: 'DELETE' });
      showPhoto(null);
      notify('Фотография удалена.');
    } catch (error) {
      if (!signal.aborted) notify(error.message);
    } finally {
      remove.disabled = submit.disabled = false;
    }
  };
}
