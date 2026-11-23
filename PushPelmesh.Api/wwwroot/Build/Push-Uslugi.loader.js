// Old Unity pages still call this function when their HTML is kept on the device.
window.createUnityInstance = () => {
  window.location.replace('/?app=web' + window.location.hash);
  return Promise.resolve(null);
};
