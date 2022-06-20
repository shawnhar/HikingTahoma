function HighlightGraph(hikename, selected)
{
  var graph = document.getElementById('graph-' + hikename);

  if (graph) {
    graph.style.color = selected ? '#95C0E0' : null;
    graph.style.backgroundColor = selected ? '#0000FF' : null;
  }
}


// Highlight trail map segments when hovering over trail names.
function OnEnterLink(document, hikename)
{
  document.getElementById('hike-' + hikename).style.visibility = 'visible';

  HighlightGraph(hikename, true);
}

function OnLeaveLink(document, hikename)
{
  document.getElementById('hike-' + hikename).style.visibility = 'hidden';

  HighlightGraph(hikename, false);
}


// Highlight trail map segments and trail names when hovering over the map.
function OnEnterImage(document, hikename)
{
  document.getElementById('hike-' + hikename).style.visibility = 'visible';
  document.getElementById('link-' + hikename).style.textDecoration = 'underline';
  document.getElementById('link-' + hikename).firstElementChild.style.color = '#0000FF';

  HighlightGraph(hikename, true);
}

function OnLeaveImage(document, hikename)
{
  document.getElementById('hike-' + hikename).style.visibility = 'hidden';
  document.getElementById('link-' + hikename).style.textDecoration = '';
  document.getElementById('link-' + hikename).firstElementChild.style.color = null;

  HighlightGraph(hikename, false);
}


function RemoveElements(collection)
{
  while (collection.length > 0) {
    collection[0].parentElement.removeChild(collection[0]);
  }
}


function UpdateHikeList(order)
{
  var hikelist = document.getElementsByClassName('hikelist')[0];
  var multicolumn = hikelist.getElementsByTagName('div')[0];
  var table = hikelist.getElementsByTagName('table')[0];

  // Look up all divs that represent hikes, while removing any obsolete graph bar divs.
  RemoveElements(table.getElementsByClassName('graphbar'));

  var items = [...multicolumn.getElementsByTagName('div'), ...table.getElementsByTagName('div')];

  RemoveElements(table.getElementsByTagName('tr'));

  // Category and key getter functions depend on the current sort order.
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
  var showGraph = true;

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
    getKey = function(item) { return Math.round(toNumber(item.getAttribute('data-elevation-gain')) / toNumber(item.getAttribute('data-length'))) };
  }
  else {
    getKey = function(item) { return getItemCategory(item) + '.' + getItemText(item) };
    showGraph = false;
  }

  // If displaying a graph, we need to know the max value of any hike.
  var maxValue = showGraph ? Math.max(...items.map(getKey)) : 0;

  // Sort hikes by the chosen attribute.
  items.sort(function(a, b) {
    var aKey = getKey(a);
    var bKey = getKey(b);

    if (aKey == bKey) {
      aKey = getItemText(a);
      bKey = getItemText(b);
    }

    return aKey < bKey ? -1 : (aKey > bKey ? 1 : 0);
  });

  // Update the DOM into sorted order.
  for (var i = items.length - 1; i >= 0; i--) {
    var item = items[i];

    if (item.className == 'listhead') {
      if (getItemCategory(item)) {
        item.style.display = 'block';

        // Move the first child inside the heading, so they'll column wrap together.
        item.appendChild(multicolumn.firstChild);
      }
      else {
        item.style.display = '';
      }
    }

    if (showGraph && item.className != 'listhead') {
      // Use table to display a graph of trail statistics.
      var itemValue = getKey(item);
      var percentage = itemValue * 100 / maxValue;

      var td1 = document.createElement('td');
      td1.setAttribute('class', 'graphheading');
      td1.appendChild(item);

      var div = document.createElement('div');
      div.setAttribute('id', item.getAttribute('id').replace('link', 'graph'));
      div.setAttribute('class', 'graphbar');
      div.setAttribute('style', 'width:' + percentage + '%');
      div.innerHTML = itemValue;

      var td2 = document.createElement('td')
      td2.appendChild(div);

      var tr = document.createElement('tr');
      tr.appendChild(td1);
      tr.appendChild(td2);

      table.insertBefore(tr, table.firstChild);
    }
    else {
      // Straightforward multi-column list of hike elements.
      multicolumn.insertBefore(item, multicolumn.firstChild);
    }
  }
}


function SortSelectorChanged(order)
{
  UpdateHikeList(order);

  localStorage.SortOrder = order;
}


// Initialize the hike order on page load.
if (localStorage.SortOrder) {
  document.getElementById('sortselector').value = localStorage.SortOrder;

  UpdateHikeList(localStorage.SortOrder);
}
