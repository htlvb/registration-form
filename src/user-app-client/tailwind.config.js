/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{vue,ts}",
  ],
  theme: {
    extend: {
      colors: {
        blue: {
          htlvb: '#183f7c'
        }
      }
    },
  },
  plugins: [],
}

