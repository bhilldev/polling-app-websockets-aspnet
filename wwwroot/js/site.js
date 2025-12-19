// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
const socket = new WebSocket("ws://" + location.host + "/poll");

// Shared state
let totalVotes = 0;

// Listen for radio selection changes
document.querySelectorAll('input[type="radio"][name="poll"]').forEach(input => {
  input.addEventListener("change", () => {
    socket.send(`choice:${input.value}`);
  });
});

socket.onmessage = (event) => {
  // Split total from choice data
  const [totalPart, choicesPart] = event.data.split("|");

  totalVotes = Number(totalPart.split(":")[1]);

  const results = choicesPart.split(",");

  results.forEach(r => {
    const [choice, count] = r.split(":");

    const el = document.querySelector(
      `.choice[data-choice="${choice}"] .count`
    );

    if (el) {
      el.textContent = `${count} votes`;
    }
  });
};
