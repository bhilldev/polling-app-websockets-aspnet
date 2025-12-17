// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
"use strict";

const socket = new WebSocket("ws://" + location.host + "/poll");

document.querySelectorAll(".choice").forEach((el, index) => {
  el.addEventListener("click", () => {
    socket.send(`choice:${index + 1}`);
  });
});

socket.onmessage = (event) => {
  // Split total from choice data
  const [totalPart, choicesPart] = event.data.split("|");

  // totalPart => "total:10"
  const totalVotes = Number(totalPart.split(":")[1]);

  // choicesPart => "1:3,2:5,3:2"
  const results = choicesPart.split(",");

  results.forEach(r => {
    const [choice, count] = r.split(":");
    const el = document.querySelector(`.choice[data-choice="${choice}"]`);

    if (el) {
      el.textContent = `Choice ${choice}: ${count} / ${totalVotes}`;

      // later: use totalVotes to set progress bar width
      // const percent = (count / totalVotes) * 100;
    }
  });
};
