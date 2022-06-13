// Highlight trail map segments when hovering over trail names.
function OnEnterLink(document, hikename)
{
  document.getElementById('hike-' + hikename).style.visibility = 'visible';
}

function OnLeaveLink(document, hikename)
{
  document.getElementById('hike-' + hikename).style.visibility = 'hidden';
}


// Highlight trail map segments and trail names when hovering over the map.
function OnEnterImage(document, hikename)
{
  document.getElementById('hike-' + hikename).style.visibility = 'visible';
  document.getElementById('link-' + hikename).style.textDecoration = 'underline';
  document.getElementById('link-' + hikename).firstElementChild.style.color = '#0000FF';
}

function OnLeaveImage(document, hikename)
{
  document.getElementById('hike-' + hikename).style.visibility = 'hidden';
  document.getElementById('link-' + hikename).style.textDecoration = '';
  document.getElementById('link-' + hikename).firstElementChild.style.color = null;
}


function SortHikes(order)
{
  var parent = document.getElementsByClassName('hikelist')[0];
  var items = [...parent.getElementsByTagName('div')];

  var getItemCategory;

  if (order == 'region') {
    getItemCategory = function(item) { return item.getAttribute('data-region') };
  }
  else if (order == 'difficulty') {
    getItemCategory = function(item) { return item.getAttribute('data-difficulty') };
  }
  else {
    getItemCategory = function(item) { return ''; };
  }

  var getItemText = function(item) {
    var links = item.getElementsByTagName('a');

    return links.length > 0 ? links[0].innerHTML : '';
  }

  var toNumber = function(value) {
    return value ? +value : 1;
  }

  var getKey;

  if (order == 'length') {
    getKey = function(item) { return toNumber(item.getAttribute('data-length')) };
  }
  else if (order == 'elevation-gain') {
    getKey = function(item) { return toNumber(item.getAttribute('data-elevation-gain')) };
  }
  else if (order == 'max-elevation') {
    getKey = function(item) { return toNumber(item.getAttribute('data-max-elevation')) };
  }
  else if (order == 'steepness') {
    getKey = function(item) { return toNumber(item.getAttribute('data-elevation-gain')) / toNumber(item.getAttribute('data-length')) };
  }
  else {
    getKey = function(item) { return getItemCategory(item) + '.' + getItemText(item) };
  }

  items.sort(function(a, b) {
    var aKey = getKey(a);
    var bKey = getKey(b);

    if (aKey == bKey) {
      aKey = getItemText(a);
      bKey = getItemText(b);
    }

    return aKey < bKey ? -1 : (aKey > bKey ? 1 : 0);
  });

  for (var i = items.length - 1; i >= 0; i--) {
    var item = items[i];

    if (item.className == 'listhead') {
      if (getItemCategory(item)) {
        item.style.display = 'block';

        // Move the first child inside the heading, so they'll column wrap together.
        item.appendChild(parent.firstChild);
      }
      else {
        item.style.display = '';
      }
    }

    parent.insertBefore(item, parent.firstChild);
  }
}


function SortSelectorChanged(order)
{
  SortHikes(order);

  localStorage.SortOrder = order;
}


// Initialize the hike order on page load.
if (localStorage.SortOrder) {
  document.getElementById('sortselector').value = localStorage.SortOrder;

  SortHikes(localStorage.SortOrder);
}
