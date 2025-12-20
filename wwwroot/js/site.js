// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
const socket = new WebSocket("ws://" + location.host + "/poll");

const choices = document.querySelectorAll(".choice");

choices.forEach(choice => {
  const radio = choice.querySelector('input[type="radio"]');

  radio.addEventListener("change", () => {
    socket.send(`choice:${radio.value}`);
  });
});

socket.onmessage = (event) => {
  // Format: total:10|1:4,2:3,3:3
  const [totalPart, choicesPart] = event.data.split("|");

  const totalVotes = Number(totalPart.split(":")[1]);
  const results = choicesPart.split(",");

  results.forEach(r => {
    const [choiceId, countStr] = r.split(":");
    const count = Number(countStr);

    const el = document.querySelector(`.choice[data-choice="${choiceId}"]`);
    if (!el) return;

    // Update text
    el.querySelector(".count").textContent =
      `${count} vote${count === 1 ? "" : "s"}`;

    // Update progress bar
    const fill = el.querySelector(".progress-fill");
    const percent = totalVotes === 0 ? 0 : (count / totalVotes) * 100;
    fill.style.width = `${percent}%`;
  });
};

